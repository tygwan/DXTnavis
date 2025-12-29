호출 스택
'''
 	[외부 코드]

>	DXnavis.dll!DXnavis.Services.NavisworksDataExtractor.TraverseAndExtractProperties(Autodesk.Navisworks.Api.ModelItem currentItem, System.Guid parentId, int level, System.Collections.Generic.List<DXnavis.Models.HierarchicalPropertyRecord> results) 줄 94	C#

 	DXnavis.dll!DXnavis.Services.NavisworksDataExtractor.TraverseAndExtractProperties(Autodesk.Navisworks.Api.ModelItem currentItem, System.Guid parentId, int level, System.Collections.Generic.List<DXnavis.Models.HierarchicalPropertyRecord> results) 줄 117	C#

 	DXnavis.dll!DXnavis.Services.NavisworksDataExtractor.TraverseAndExtractProperties(Autodesk.Navisworks.Api.ModelItem currentItem, System.Guid parentId, int level, System.Collections.Generic.List<DXnavis.Models.HierarchicalPropertyRecord> results) 줄 117	C#

 	DXnavis.dll!DXnavis.ViewModels.DXwindowViewModel.OnTreeNodeSelectionChanged.AnonymousMethod__0() 줄 764	C#

 	[외부 코드]	

'''
'''
예외처리되지않음 내용
                        var record = new HierarchicalPropertyRecord(

                            objectId: currentId,

                            parentId: parentId,

                            level: level,

                            displayName: displayName,

                            category: category.DisplayName ?? string.Empty,

                            propertyName: property.DisplayName ?? string.Empty,

                            propertyValue: property.Value?.ToString() ?? string.Empty

                        );
'''
System.AccessViolationException: '보호된 메모리를 읽거나 쓰려고 했습니다. 대부분 이러한 경우는 다른 메모리가 손상되었음을 나타냅니다.'

'''
vs의 자동 탭에서 발생한 내용
+		record	{DXnavis.Models.HierarchicalPropertyRecord}	DXnavis.Models.HierarchicalPropertyRecord

+		results	Count = 62	System.Collections.Generic.List<DXnavis.Models.HierarchicalPropertyRecord>

		this	{DXnavis.Services.NavisworksDataExtractor}	DXnavis.Services.NavisworksDataExtractor

'''