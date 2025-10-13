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
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                // 설정 UI 표시
                var viewModel = new SettingsViewModel();
                var view = new SettingsView { DataContext = viewModel };

                bool? dialogResult = view.ShowDialog();

                if (dialogResult == true)
                {
                    LoggingService.LogInfo("설정이 저장되었습니다", "DXrevit");
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
                LoggingService.LogError("설정 커맨드 실행 실패", "DXrevit", ex);
                return Result.Failed;
            }
        }
    }
}
