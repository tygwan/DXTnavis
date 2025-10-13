using Autodesk.Revit.UI;
using DXBase.Services;
using System;
using System.Reflection;

namespace DXrevit
{
    /// <summary>
    /// Revit 애드인 진입점
    /// </summary>
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string debugLogPath = @"C:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\Errorlog\DXrevit_startup.log";

            try
            {
                // 디버그: 시작 시각 기록
                System.IO.File.AppendAllText(debugLogPath,
                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OnStartup 시작\n");

                // 1단계: 설정 로드 (로깅 없이)
                System.IO.File.AppendAllText(debugLogPath, "1. ConfigurationService.LoadSettings() 호출\n");
                var settings = ConfigurationService.LoadSettings();
                System.IO.File.AppendAllText(debugLogPath, $"   설정 로드 완료: LogFilePath = {settings.LogFilePath}\n");

                // 2단계: 로깅 초기화
                System.IO.File.AppendAllText(debugLogPath, "2. LoggingService.Initialize() 호출\n");
                LoggingService.Initialize(settings.LogFilePath);
                LoggingService.LogInfo("DXrevit 애드인 시작", "DXrevit");
                System.IO.File.AppendAllText(debugLogPath, "   로깅 초기화 완료\n");

                // 3단계: 리본 UI 생성
                System.IO.File.AppendAllText(debugLogPath, "3. CreateRibbonUI() 호출\n");
                CreateRibbonUI(application);
                System.IO.File.AppendAllText(debugLogPath, "   리본 UI 생성 완료\n");

                LoggingService.LogInfo("DXrevit 리본 UI 생성 완료", "DXrevit");
                System.IO.File.AppendAllText(debugLogPath, "✅ OnStartup 성공\n");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // 디버그: 예외 상세 정보 기록
                System.IO.File.AppendAllText(debugLogPath,
                    $"❌ 예외 발생: {ex.GetType().Name}\n" +
                    $"   메시지: {ex.Message}\n" +
                    $"   스택: {ex.StackTrace}\n");

                // 로깅 초기화 실패 시에도 TaskDialog로 오류 표시
                try
                {
                    LoggingService.LogError("DXrevit 초기화 실패", "DXrevit", ex);
                }
                catch
                {
                    TaskDialog.Show("DXrevit 초기화 실패",
                        $"오류: {ex.Message}\n\n로그 파일: {debugLogPath}");
                }
                return Result.Failed;
            }
        }

        private void CreateRibbonUI(UIControlledApplication application)
        {
            // 리본 탭 생성 (중복 방지)
            string tabName = "DX Platform";
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // 탭이 이미 존재하는 경우 무시
            }

            // 리본 패널 생성
            RibbonPanel panel = application.CreateRibbonPanel(tabName, "데이터 관리");

            // 버튼 추가
            AddSnapshotButton(panel);
            AddParameterSetupButton(panel);
            AddSettingsButton(panel);
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            LoggingService.LogInfo("DXrevit 애드인 종료", "DXrevit");
            return Result.Succeeded;
        }

        private void AddSnapshotButton(RibbonPanel panel)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "SnapshotButton",
                "스냅샷 저장",
                assemblyPath,
                "DXrevit.Commands.SnapshotCommand");

            PushButton button = panel.AddItem(buttonData) as PushButton;
            button.ToolTip = "현재 BIM 모델의 스냅샷을 데이터베이스에 저장합니다.";
            button.LongDescription = "모든 객체, 속성, 관계를 추출하여 중앙 데이터베이스에 저장합니다. 설계 변경 이력 추적의 기준점이 됩니다.";
        }

        private void AddParameterSetupButton(RibbonPanel panel)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "ParameterSetupButton",
                "매개변수 설정",
                assemblyPath,
                "DXrevit.Commands.ParameterSetupCommand");

            PushButton button = panel.AddItem(buttonData) as PushButton;
            button.ToolTip = "공유 매개변수를 프로젝트에 추가합니다.";
        }

        private void AddSettingsButton(RibbonPanel panel)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "SettingsButton",
                "설정",
                assemblyPath,
                "DXrevit.Commands.SettingsCommand");

            PushButton button = panel.AddItem(buttonData) as PushButton;
            button.ToolTip = "DXrevit 설정을 변경합니다.";
        }
    }
}
