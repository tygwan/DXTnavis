using System;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;

namespace DXTnavis.Services
{
    /// <summary>
    /// Navisworks 검색 세트(SearchSet)를 생성하는 서비스
    /// 속성 기반 필터링을 통해 객체를 그룹화합니다.
    /// </summary>
    public class SetCreationService
    {
        /// <summary>
        /// 특정 속성 조건에 맞는 검색 세트를 생성합니다.
        /// </summary>
        /// <param name="folderName">세트를 저장할 폴더 이름</param>
        /// <param name="setName">생성할 세트의 이름</param>
        /// <param name="propertyCategory">속성 카테고리</param>
        /// <param name="propertyName">속성 이름</param>
        /// <param name="propertyValue">속성 값</param>
        public void CreateSearchSetFromProperty(
            string folderName,
            string setName,
            string propertyCategory,
            string propertyName,
            string propertyValue)
        {
            // 입력값 유효성 검사
            if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(setName))
                throw new ArgumentException("폴더와 세트 이름은 비워둘 수 없습니다.");

            if (string.IsNullOrWhiteSpace(propertyCategory) || string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("속성 카테고리와 이름은 비워둘 수 없습니다.");

            Document doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성화된 문서가 없습니다.");

            // 1. 검색 객체(Search) 및 조건(SearchCondition) 정의
            var search = new Search();
            search.Selection.SelectAll(); // 검색 범위: 문서 전체

            // 조건: 지정된 카테고리와 이름의 속성을 가지며, 그 값이 일치하는 객체
            var condition = SearchCondition
                .HasPropertyByDisplayName(propertyCategory, propertyName)
                .EqualValue(VariantData.FromDisplayString(propertyValue));

            search.SearchConditions.Add(condition);

            // 2. 검색 실행하여 결과를 SelectionSet으로 변환
            ModelItemCollection foundItems = search.FindAll(doc, false);
            SelectionSet selectionSet = new SelectionSet(foundItems);

            // 3. DocumentSelectionSets를 통해 검색 세트 저장
            DocumentSelectionSets docSets = doc.SelectionSets;

            // 4. 폴더를 찾거나 새로 생성
            FolderItem targetFolder = GetOrCreateFolder(docSets, folderName);

            // 5. SelectionSet을 저장하고 폴더로 이동
            // Note: Navisworks API는 먼저 루트에 추가한 후 폴더로 이동하는 방식
            int addedIndex = docSets.Value.IndexOf(selectionSet);
            if (addedIndex < 0)
            {
                // SelectionSet이 아직 저장되지 않은 경우
                docSets.AddCopy(selectionSet);
                addedIndex = docSets.Value.Count - 1;
            }

            if (addedIndex >= 0 && addedIndex < docSets.Value.Count)
            {
                var savedItem = docSets.Value[addedIndex];
                savedItem.DisplayName = setName;

                // 폴더로 이동
                if (targetFolder != null)
                {
                    targetFolder.Children.Add(savedItem);
                    // 루트에서 제거
                    docSets.Value.RemoveAt(addedIndex);
                }
            }
        }

        /// <summary>
        /// 지정된 이름의 폴더를 찾거나, 없으면 새로 생성하는 헬퍼 메서드
        /// </summary>
        private FolderItem GetOrCreateFolder(DocumentSelectionSets docSets, string folderName)
        {
            // 기존 폴더 검색
            var existingFolder = docSets.RootItem.Children
                .OfType<FolderItem>()
                .FirstOrDefault(item => item.DisplayName == folderName);

            if (existingFolder != null)
            {
                return existingFolder;
            }
            else
            {
                // 새 폴더 생성
                var newFolder = new FolderItem();
                newFolder.DisplayName = folderName;
                docSets.Value.Add(newFolder);
                return newFolder;
            }
        }
    }
}
