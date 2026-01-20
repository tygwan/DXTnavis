using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;

namespace DXTnavis.Utils
{
    /// <summary>
    /// ObservableCollection 확장 메서드 - 대용량 데이터 배치 추가 지원
    /// v0.8.0: UI 프리징 방지를 위한 배치 업데이트
    /// </summary>
    public static class ObservableCollectionExtensions
    {
        // 캐싱된 리플렉션 정보 (성능 최적화)
        private static readonly Dictionary<Type, FieldInfo> _itemsFieldCache = new Dictionary<Type, FieldInfo>();
        private static readonly Dictionary<Type, MethodInfo> _onCollectionChangedCache = new Dictionary<Type, MethodInfo>();
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// 대용량 데이터를 효율적으로 추가 (UI 업데이트 최소화)
        /// CollectionChanged 이벤트를 일시 중단하고 마지막에 Reset 이벤트 한 번만 발생
        /// </summary>
        public static void AddRangeSuppressed<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (items == null) return;

            var internalList = GetInternalList(collection);
            if (internalList != null)
            {
                foreach (var item in items)
                {
                    internalList.Add(item);
                }

                // Reset 이벤트 한 번만 발생
                RaiseCollectionReset(collection);
                return;
            }

            // Fallback: 일반 Add 사용 (느림)
            System.Diagnostics.Debug.WriteLine("[WARN] AddRangeSuppressed: Fallback to slow path");
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        /// <summary>
        /// 컬렉션을 지우고 새 데이터로 대체 (UI 업데이트 최소화)
        /// 445,730개 아이템도 UI 프리징 없이 처리
        /// </summary>
        public static void ReplaceAll<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            var internalList = GetInternalList(collection);
            if (internalList != null)
            {
                // 내부 리스트 직접 조작 (이벤트 발생 안함)
                internalList.Clear();

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        internalList.Add(item);
                    }
                }

                // Reset 이벤트 한 번만 발생
                RaiseCollectionReset(collection);
                System.Diagnostics.Debug.WriteLine($"[ReplaceAll] 배치 모드 성공: {internalList.Count} items");
                return;
            }

            // Fallback: 일반 방식 (느림)
            System.Diagnostics.Debug.WriteLine("[WARN] ReplaceAll: Fallback to slow path");
            collection.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    collection.Add(item);
                }
            }
        }

        /// <summary>
        /// Collection의 내부 items 필드에 접근
        /// </summary>
        private static IList<T> GetInternalList<T>(ObservableCollection<T> collection)
        {
            try
            {
                var collectionType = typeof(Collection<T>);
                FieldInfo itemsField;

                lock (_cacheLock)
                {
                    if (!_itemsFieldCache.TryGetValue(collectionType, out itemsField))
                    {
                        // Collection<T>의 private 필드 "items"에 접근
                        itemsField = collectionType.GetField("items",
                            BindingFlags.Instance | BindingFlags.NonPublic);

                        _itemsFieldCache[collectionType] = itemsField;
                    }
                }

                if (itemsField != null)
                {
                    return itemsField.GetValue(collection) as IList<T>;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] GetInternalList failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// CollectionChanged Reset 이벤트 발생
        /// </summary>
        private static void RaiseCollectionReset<T>(ObservableCollection<T> collection)
        {
            try
            {
                var collectionType = typeof(ObservableCollection<T>);
                MethodInfo onCollectionChangedMethod;

                lock (_cacheLock)
                {
                    if (!_onCollectionChangedCache.TryGetValue(collectionType, out onCollectionChangedMethod))
                    {
                        // ObservableCollection<T>의 OnCollectionChanged 메서드
                        onCollectionChangedMethod = collectionType.GetMethod("OnCollectionChanged",
                            BindingFlags.Instance | BindingFlags.NonPublic,
                            null,
                            new Type[] { typeof(NotifyCollectionChangedEventArgs) },
                            null);

                        _onCollectionChangedCache[collectionType] = onCollectionChangedMethod;
                    }
                }

                if (onCollectionChangedMethod != null)
                {
                    onCollectionChangedMethod.Invoke(collection, new object[]
                    {
                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WARN] OnCollectionChanged method not found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] RaiseCollectionReset failed: {ex.Message}");
            }
        }
    }
}
