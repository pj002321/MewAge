/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System.Collections.Generic;
using UnityEngine;

namespace FancyScrollView
{
    /// <summary>
    /// 스크롤 뷰를 구현하기위한 추상 기본 클래스.
    /// 무한 스크롤 및 스냅을 지원합니다.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> が不要な場合は
    /// 代わりに <see cref="FancyScrollView{TItemData}"/> を使用します.
    /// </summary>
    /// <typeparam name="TItemData">항목의 데이터 형식.</typeparam>
    /// <typeparam name="TContext"><see cref="Context"/> の型.</typeparam>
    public abstract class FancyScrollView<TItemData, TContext> : MonoBehaviour where TContext : class, new()
    {
        /// <summary>
        /// 셀 사이의 간격.
        /// </summary>
        [SerializeField, Range(1e-2f, 1f)] protected float cellInterval = 0.2f;

        /// <summary>
        /// 스크롤 위치 기준.
        /// </summary>
        /// <remarks>
        /// 예를 들어 <c>0.5</c> 를 지정하고 스크롤 위치가 <c>0</c> 이면 중앙에 첫 번째 셀이 배치됩니다.
        /// </remarks>
        [SerializeField, Range(0f, 1f)] protected float scrollOffset = 0.5f;

        /// <summary>
        /// 셀을 순환하여 배치할지 여부.
        /// </summary>
        /// <remarks>
        /// <c>true</c> 로 설정하면 마지막 셀 다음에 첫 번째 셀, 첫 번째 셀 앞에 마지막 셀이 나열됩니다.
        /// 무한 스크롤을 구현하려면 <c>true</c> 를 지정하십시오.
        /// </remarks>
        [SerializeField] protected bool loop = false;

        /// <summary>
        ///셀의 부모 요소가 됨 <c>Transform</c>.
        /// </summary>
        [SerializeField] protected Transform cellContainer = default;

        private readonly IList<FancyCell<TItemData, TContext>> pool = new List<FancyCell<TItemData, TContext>>();

        /// <summary>
        /// 초기화되었는지 여부.
        /// </summary>
        protected bool initialized;

        /// <summary>
        /// 현재 스크롤 위치.
        /// </summary>
        protected float currentPosition;

        /// <summary>
        /// 셀의 Prefab.
        /// </summary>
        protected abstract GameObject CellPrefab { get; }

        /// <summary>
        /// 항목 목록 데이터.
        /// </summary>
        protected IList<TItemData> ItemsSource { get; set; } = new List<TItemData>();

        /// <summary>
        /// <typeparamref name="TContext"/> のインスタンス.
        /// 셀과 스크롤 뷰간에 동일한 인스턴스가 공유됩니다. 정보를 전달하거나 상태를 유지하는 데 사용됩니다.
        /// </summary>
        protected TContext Context { get; } = new TContext();

        /// <summary>
        /// 초기화를 수행합니다.
        /// </summary>
        /// <remarks>
        /// 처음에 셀이 생성되기 직전에 호출됩니다.
        /// </remarks>
        public virtual void Initialize()
        { }

        /// <summary>
        /// 전달 된 항목 목록을 기반으로 표시 내용을 업데이트합니다.
        /// </summary>
        /// <param name="itemsSource">アイテム一覧.</param>
        protected virtual void UpdateContents(IList<TItemData> itemsSource)
        {
            ItemsSource = itemsSource;
            Refresh();
        }

        /// <summary>
        /// 셀 레이아웃을 강제로 업데이트합니다.
        /// </summary>
        protected virtual void Relayout() => UpdatePosition(currentPosition, false);

        /// <summary>
        /// 셀의 레이아웃과 표시 내용을 강제로 업데이트합니다.
        /// </summary>
        protected virtual void Refresh() => UpdatePosition(currentPosition, true);

        protected virtual void newRefresh() => UpdatePosition(0, true);

        /// <summary>
        /// 스크롤 위치를 업데이트합니다.
        /// </summary>
        /// <param name="position">스크롤 위치..</param>
        protected virtual void UpdatePosition(float position) => UpdatePosition(position, false);

        private void UpdatePosition(float position, bool forceRefresh)
        {
            if (!initialized)
            {
                Initialize();
                initialized = true;
            }

            currentPosition = position;
            var p = position - scrollOffset / cellInterval;
            var firstIndex = Mathf.CeilToInt(p);
            var firstPosition = (Mathf.Ceil(p) - p) * cellInterval;

            if (firstPosition + pool.Count * cellInterval < 1f)
            {
                ResizePool(firstPosition);
            }

            UpdateCells(firstPosition, firstIndex, forceRefresh);
        }

        private void ResizePool(float firstPosition)
        {
            Debug.Assert(CellPrefab != null);
            Debug.Assert(cellContainer != null);

            var addCount = Mathf.CeilToInt((1f - firstPosition) / cellInterval) - pool.Count;
            for (var i = 0; i < addCount; i++)
            {
                var cell = Instantiate(CellPrefab, cellContainer).GetComponent<FancyCell<TItemData, TContext>>();
                if (cell == null)
                {
                    throw new MissingComponentException(string.Format(
                        "FancyCell<{0}, {1}> component not found in {2}.",
                        typeof(TItemData).FullName, typeof(TContext).FullName, CellPrefab.name));
                }

                cell.SetContext(Context);
                cell.Initialize();
                cell.SetVisible(false);
                pool.Add(cell);
            }
        }

        private void UpdateCells(float firstPosition, int firstIndex, bool forceRefresh)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var index = firstIndex + i;
                var position = firstPosition + i * cellInterval;
                var cell = pool[CircularIndex(index, pool.Count)];

                if (loop)
                {
                    index = CircularIndex(index, ItemsSource.Count);
                }

                if (index < 0 || index >= ItemsSource.Count || position > 1f)
                {
                    cell.SetVisible(false);
                    continue;
                }

                if (forceRefresh || cell.Index != index || !cell.IsVisible)
                {
                    cell.Index = index;
                    cell.SetVisible(true);
                    cell.UpdateContent(ItemsSource[index], true, index, ItemsSource.Count);
                }

                cell.UpdatePosition(position);
            }
        }

        private int CircularIndex(int i, int size) => size < 1 ? 0 : i < 0 ? size - 1 + (i + 1) % size : i % size;

#if UNITY_EDITOR
        private bool cachedLoop;
        private float cachedCellInterval, cachedScrollOffset;

        private void LateUpdate()
        {
            if (cachedLoop != loop ||
                cachedCellInterval != cellInterval ||
                cachedScrollOffset != scrollOffset)
            {
                cachedLoop = loop;
                cachedCellInterval = cellInterval;
                cachedScrollOffset = scrollOffset;

                UpdatePosition(currentPosition);
            }
        }

#endif
    }

    /// <summary>
    /// <see cref="FancyScrollView{TItemData}"/> 컨텍스트 클래스.
    /// </summary>
    public sealed class NullContext
    { }

    /// <summary>
    /// 스크롤 뷰를 구현하기위한 추상 기본 클래스
    /// 무한 스크롤 및 스냅을 지원합니다.
    /// </summary>
    /// <typeparam name="TItemData"></typeparam>
    /// <seealso cref="FancyScrollView{TItemData, TContext}"/>
    public abstract class FancyScrollView<TItemData> : FancyScrollView<TItemData, NullContext>
    { }
}
