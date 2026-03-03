using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Phase 26: Navisworks 내장 FBX Export 플러그인을 사용한 mesh 추출
    /// GenerateSimplePrimitives()로 tessellation 실패한 객체의 fallback
    /// 내부 tessellator가 파라메트릭/B-Rep geometry도 삼각형으로 변환
    /// </summary>
    public class FbxExportService
    {
        private const string FBX_PLUGIN_ID =
            "NativeExportPluginAdaptor_LcFbxExporterPlugin_Export.Navisworks";

        #region Events

        public event EventHandler<string> StatusChanged;
        public event EventHandler<int> ProgressChanged;

        #endregion

        #region Public API

        /// <summary>
        /// gap_supplemented 객체만 선별하여 FBX export
        /// 나머지 객체를 숨기고, 대상 객체만 visible 상태에서 FBX 생성
        /// </summary>
        /// <param name="targetItems">FBX로 export할 ModelItem 목록 (gap_supplemented 객체)</param>
        /// <param name="outputFbxPath">FBX 출력 파일 경로</param>
        /// <returns>성공 여부</returns>
        public bool ExportTargetItemsAsFbx(
            IEnumerable<ModelItem> targetItems,
            string outputFbxPath)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
            {
                OnStatusChanged("활성 문서가 없습니다.");
                return false;
            }

            var targetSet = new HashSet<ModelItem>(targetItems);
            if (targetSet.Count == 0)
            {
                OnStatusChanged("FBX export 대상 객체가 없습니다.");
                return false;
            }

            OnStatusChanged(string.Format("FBX export 준비: {0}개 객체", targetSet.Count));

            // FBX plugin 찾기
            var pluginRecord = Application.Plugins.FindPlugin(FBX_PLUGIN_ID);
            if (pluginRecord == null)
            {
                OnStatusChanged("FBX export 플러그인을 찾을 수 없습니다: " + FBX_PLUGIN_ID);
                Debug.WriteLine("[FbxExport] Plugin not found: " + FBX_PLUGIN_ID);
                return false;
            }

            if (!pluginRecord.IsLoaded)
            {
                try
                {
                    pluginRecord.LoadPlugin();
                }
                catch (Exception ex)
                {
                    OnStatusChanged("FBX 플러그인 로드 실패: " + ex.Message);
                    Debug.WriteLine("[FbxExport] Plugin load error: " + ex);
                    return false;
                }
            }

            // 출력 폴더 확인
            string outputDir = Path.GetDirectoryName(outputFbxPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            // ── 대상 객체만 visible하게 설정 (Hide/Unhide 워크어라운드) ──
            // FBX export는 visible 객체만 포함하므로, 대상만 보이게 하고 나머지 숨김
            ModelItemCollection itemsToHide = null;
            try
            {
                OnStatusChanged("FBX export: 대상 객체 격리 중...");
                OnProgressChanged(10);

                // target 객체 + 모든 조상 노드를 visible 유지 목록에 추가
                // Navisworks에서 부모가 hidden이면 자식도 자동 hidden되므로
                // 조상 체인 전체를 보이게 유지해야 함
                var keepVisible = new HashSet<ModelItem>(targetSet);
                foreach (var target in targetSet)
                {
                    var ancestor = target.Parent;
                    while (ancestor != null)
                    {
                        keepVisible.Add(ancestor);
                        ancestor = ancestor.Parent;
                    }
                }
                Debug.WriteLine(string.Format("[FbxExport] keepVisible: {0}개 (target {1} + ancestors {2})",
                    keepVisible.Count, targetSet.Count, keepVisible.Count - targetSet.Count));

                // 전체 아이템 수집
                var allItems = new ModelItemCollection();
                foreach (var model in doc.Models)
                {
                    foreach (var item in model.RootItem.DescendantsAndSelf)
                    {
                        allItems.Add(item);
                    }
                }

                // keepVisible에 없는 아이템만 숨기기
                itemsToHide = new ModelItemCollection();
                foreach (ModelItem item in allItems)
                {
                    if (!keepVisible.Contains(item))
                    {
                        itemsToHide.Add(item);
                    }
                }
                Debug.WriteLine(string.Format("[FbxExport] Hiding {0} items, keeping {1} visible",
                    itemsToHide.Count, allItems.Count - itemsToHide.Count));

                doc.Models.SetHidden(itemsToHide, true);
                OnProgressChanged(20);

                // ── FBX Export 실행 ──
                OnStatusChanged(string.Format("FBX export 실행 중: {0}개 객체 → {1}",
                    targetSet.Count, Path.GetFileName(outputFbxPath)));

                bool success = ExecuteFbxExport(pluginRecord, outputFbxPath);

                OnProgressChanged(80);

                if (success && File.Exists(outputFbxPath))
                {
                    var fileInfo = new FileInfo(outputFbxPath);
                    OnStatusChanged(string.Format("FBX export 완료: {0} ({1:F1} MB)",
                        Path.GetFileName(outputFbxPath), fileInfo.Length / 1048576.0));
                    return true;
                }
                else
                {
                    OnStatusChanged("FBX export 실패: 파일이 생성되지 않았습니다.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnStatusChanged("FBX export 오류: " + ex.Message);
                Debug.WriteLine("[FbxExport] Error: " + ex);
                return false;
            }
            finally
            {
                // ── 반드시 visibility 복원 ──
                if (itemsToHide != null && itemsToHide.Count > 0)
                {
                    try
                    {
                        doc.Models.SetHidden(itemsToHide, false);
                        OnStatusChanged("객체 visibility 복원 완료");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[FbxExport] Visibility restore error: " + ex.Message);
                    }
                }
                OnProgressChanged(100);
            }
        }

        /// <summary>
        /// 전체 모델을 FBX로 export (모든 visible 객체 포함)
        /// </summary>
        /// <param name="outputFbxPath">FBX 출력 파일 경로</param>
        /// <returns>성공 여부</returns>
        public bool ExportFullModelAsFbx(string outputFbxPath)
        {
            var pluginRecord = Application.Plugins.FindPlugin(FBX_PLUGIN_ID);
            if (pluginRecord == null)
            {
                OnStatusChanged("FBX export 플러그인을 찾을 수 없습니다.");
                return false;
            }

            if (!pluginRecord.IsLoaded)
            {
                try { pluginRecord.LoadPlugin(); }
                catch (Exception ex)
                {
                    OnStatusChanged("FBX 플러그인 로드 실패: " + ex.Message);
                    return false;
                }
            }

            string outputDir = Path.GetDirectoryName(outputFbxPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            try
            {
                OnStatusChanged("전체 모델 FBX export 실행 중...");
                bool success = ExecuteFbxExport(pluginRecord, outputFbxPath);

                if (success && File.Exists(outputFbxPath))
                {
                    var fileInfo = new FileInfo(outputFbxPath);
                    OnStatusChanged(string.Format("FBX export 완료: {0} ({1:F1} MB)",
                        Path.GetFileName(outputFbxPath), fileInfo.Length / 1048576.0));
                    return true;
                }

                OnStatusChanged("FBX export 실패");
                return false;
            }
            catch (Exception ex)
            {
                OnStatusChanged("FBX export 오류: " + ex.Message);
                Debug.WriteLine("[FbxExport] Full model error: " + ex);
                return false;
            }
        }

        /// <summary>
        /// FBX export 플러그인 사용 가능 여부 확인
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                var pluginRecord = Application.Plugins.FindPlugin(FBX_PLUGIN_ID);
                if (pluginRecord != null)
                {
                    Debug.WriteLine("[FbxExport] Plugin found: " + FBX_PLUGIN_ID);
                    return true;
                }

                // 진단: 등록된 Export 플러그인 목록 출력
                Debug.WriteLine("[FbxExport] Plugin NOT found: " + FBX_PLUGIN_ID);
                Debug.WriteLine("[FbxExport] Searching registered plugins for 'Export' or 'Fbx'...");
                foreach (var p in Application.Plugins.PluginRecords)
                {
                    string id = p.Id ?? "(null)";
                    string name = p.DisplayName ?? "(null)";
                    if (id.IndexOf("Export", StringComparison.OrdinalIgnoreCase) >= 0
                        || id.IndexOf("Fbx", StringComparison.OrdinalIgnoreCase) >= 0
                        || id.IndexOf("fbx", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("FBX", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.WriteLine(string.Format("[FbxExport]   MATCH: Id={0}, Name={1}, IsLoaded={2}",
                            id, name, p.IsLoaded));
                    }
                }
                // 전체 플러그인 수 출력
                int totalCount = 0;
                foreach (var p in Application.Plugins.PluginRecords) totalCount++;
                Debug.WriteLine("[FbxExport] Total registered plugins: " + totalCount);

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[FbxExport] IsAvailable error: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// FBX Export 플러그인 실행 (Reflection 방식)
        /// NativeExportPluginAdaptor 타입은 컴파일 타임에 접근 불가하므로
        /// Reflection으로 Execute(string) 메서드를 호출
        /// </summary>
        private bool ExecuteFbxExport(PluginRecord pluginRecord, string outputPath)
        {
            var plugin = pluginRecord.LoadedPlugin;
            if (plugin == null)
            {
                Debug.WriteLine("[FbxExport] LoadedPlugin is null");
                return false;
            }

            try
            {
                Debug.WriteLine("[FbxExport] Executing via Reflection: " + plugin.GetType().FullName);
                plugin.GetType().InvokeMember(
                    "Execute",
                    System.Reflection.BindingFlags.InvokeMethod
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance,
                    null,
                    plugin,
                    new object[] { outputPath });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[FbxExport] Reflection Execute failed: " + ex.Message);
                OnStatusChanged("FBX plugin 실행 실패: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region Event Helpers

        private void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
            Debug.WriteLine("[FbxExport] " + message);
        }

        private void OnProgressChanged(int percentage)
        {
            ProgressChanged?.Invoke(this, percentage);
        }

        #endregion
    }
}
