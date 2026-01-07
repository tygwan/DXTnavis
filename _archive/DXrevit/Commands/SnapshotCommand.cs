using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DXBase.Services;
using DXrevit.ViewModels;
using DXrevit.Views;
using System;

namespace DXrevit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class SnapshotCommand : IExternalCommand
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

                // 문서가 저장되어 있는지 확인
                if (!doc.IsValidObject || string.IsNullOrEmpty(doc.PathName))
                {
                    TaskDialog.Show("오류", "문서를 먼저 저장해주세요.");
                    return Result.Cancelled;
                }

                // 스냅샷 UI 표시 (Phase B - V2)
                var viewModel = new SnapshotViewModelV2(doc);
                var view = new SnapshotView { DataContext = viewModel };

                bool? dialogResult = view.ShowDialog();

                if (dialogResult == true)
                {
                    return Result.Succeeded;
                }
                else
                {
                    return Result.Cancelled;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                LoggingService.LogError("스냅샷 커맨드 실행 실패", "DXrevit", ex);
                return Result.Failed;
            }
        }
    }
}
