using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DXBase.Services;
using System;
using System.IO;
using System.Reflection;

namespace DXrevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ParameterSetupCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;
                Autodesk.Revit.ApplicationServices.Application app = commandData.Application.Application;

                // SharedParameters.txt 경로
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string sharedParamFile = Path.Combine(assemblyPath, "Resources", "SharedParameters.txt");

                if (!File.Exists(sharedParamFile))
                {
                    TaskDialog.Show("오류", "공유 매개변수 파일을 찾을 수 없습니다.");
                    return Result.Failed;
                }

                // 공유 매개변수 파일 설정
                app.SharedParametersFilename = sharedParamFile;
                DefinitionFile defFile = app.OpenSharedParameterFile();

                if (defFile == null)
                {
                    TaskDialog.Show("오류", "공유 매개변수 파일을 열 수 없습니다.");
                    return Result.Failed;
                }

                // 그룹 찾기
                DefinitionGroup defGroup = defFile.Groups.get_Item("DX_Platform");
                if (defGroup == null)
                {
                    TaskDialog.Show("오류", "DX_Platform 그룹을 찾을 수 없습니다.");
                    return Result.Failed;
                }

                // 트랜잭션 시작
                using (Transaction trans = new Transaction(doc, "공유 매개변수 추가"))
                {
                    trans.Start();

                    // 적용할 카테고리
                    CategorySet categories = app.Create.NewCategorySet();
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Walls));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Doors));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralColumns));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralFraming));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Floors));

                    // ActivityId 매개변수 추가
                    ExternalDefinition activityIdDef = defGroup.Definitions.get_Item("ActivityId") as ExternalDefinition;
                    if (activityIdDef != null)
                    {
                        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
                        doc.ParameterBindings.Insert(activityIdDef, binding, GroupTypeId.Data);
                    }

                    // Cost 매개변수 추가
                    ExternalDefinition costDef = defGroup.Definitions.get_Item("Cost") as ExternalDefinition;
                    if (costDef != null)
                    {
                        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
                        doc.ParameterBindings.Insert(costDef, binding, GroupTypeId.Data);
                    }

                    // Material 매개변수 추가
                    ExternalDefinition materialDef = defGroup.Definitions.get_Item("Material") as ExternalDefinition;
                    if (materialDef != null)
                    {
                        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
                        doc.ParameterBindings.Insert(materialDef, binding, GroupTypeId.Data);
                    }

                    trans.Commit();
                }

                TaskDialog.Show("성공", "공유 매개변수가 프로젝트에 추가되었습니다.");
                LoggingService.LogInfo("공유 매개변수 추가 완료", "DXrevit");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                LoggingService.LogError("공유 매개변수 추가 실패", "DXrevit", ex);
                return Result.Failed;
            }
        }
    }
}
