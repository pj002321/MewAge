/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using UnityEngine;
using UnityEngine.UI;

namespace FancyScrollView.Example08
{
    internal class Cell : FancyGridViewCell<ItemData, Context>
    {
        [SerializeField] private Text message = default;
        [SerializeField] private Image image = default;
        [SerializeField] private Button button = default;

        public override void Initialize()
        {
            button.onClick.AddListener(() => Context.OnCellClicked?.Invoke(Index));
        }

        public override void UpdateContent(ItemData itemData, bool isLast, int index = 0, int count = 0)
        {
            message.text = itemData.Index.ToString();

            var selected = Context.SelectedIndex == Index;
            image.color = selected
                ? new Color32(0, 255, 255, 100)
                : new Color32(255, 255, 255, 77);
        }

        protected override void UpdatePosition(float normalizedPosition, float localPosition)
        {
            base.UpdatePosition(normalizedPosition, localPosition);

            var wave = Mathf.Sin(normalizedPosition * Mathf.PI * 2) * 65;
            transform.localPosition += Vector3.right * wave;
        }
    }
}
