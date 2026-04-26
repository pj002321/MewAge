/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using UnityEngine;
using System.Collections.Generic;

namespace FancyScrollView.Example09
{
    internal class ScrollView : FancyScrollView<ItemData>
    {
        [SerializeField] private Scroller scroller = default;
        [SerializeField] private GameObject cellPrefab = default;

        protected override GameObject CellPrefab => cellPrefab;

        public override void Initialize()
        {
            base.Initialize();
            scroller.OnValueChanged(UpdatePosition);
        }

        public void UpdateData(IList<ItemData> items)
        {
            UpdateContents(items);
            scroller.SetTotalCount(items.Count);
        }
    }
}
