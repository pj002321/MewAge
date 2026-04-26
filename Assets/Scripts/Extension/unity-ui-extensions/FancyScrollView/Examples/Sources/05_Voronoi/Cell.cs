/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using UnityEngine;
using UnityEngine.UI;

namespace FancyScrollView.Example05
{
    [ExecuteInEditMode]
    internal class Cell : FancyCell<ItemData, Context>
    {
        [SerializeField] private Animator scrollAnimator = default;
        [SerializeField] private Animator selectAnimator = default;
        [SerializeField] private Text message = default;
        [SerializeField] private Image image = default;
        [SerializeField] private Button button = default;
        [SerializeField] private RectTransform rectTransform = default;
        [SerializeField, HideInInspector] private Vector3 position = default;

        private static class AnimatorHash
        {
            public static readonly int Scroll = Animator.StringToHash("scroll");
            public static readonly int In = Animator.StringToHash("in");
            public static readonly int Out = Animator.StringToHash("out");
        }

        private float hash;
        private bool currentSelection;
        private float updateSelectionTime;

        public override void Initialize()
        {
            hash = Random.value * 100f;
            button.onClick.AddListener(() => Context.OnCellClicked?.Invoke(Index));

            Context.UpdateCellState += () =>
            {
                var siblingIndex = rectTransform.GetSiblingIndex();
                var t = Mathf.Clamp01((Time.time - updateSelectionTime) * (1f / 0.3f));
                var selectAnimation = currentSelection ? t : 1f - t;
                var position = IsVisible
                    ? this.position + GetFluctuation()
                    : rectTransform.rect.size.x * 10f * Vector3.left;

                Context.SetCellState(siblingIndex, Index, position.x, position.y, selectAnimation);
            };
        }

        private void LateUpdate()
        {
            image.rectTransform.localPosition = position + GetFluctuation();
        }

        private Vector3 GetFluctuation()
        {
            var fluctX = Mathf.Sin(Time.time + hash * 40) * 10;
            var fluctY = Mathf.Sin(Time.time + hash) * 10;
            return new Vector3(fluctX, fluctY, 0f);
        }

        public override void UpdateContent(ItemData cellData, bool isLast, int index = 0, int count = 0)
        {
            message.text = cellData.Message;
            SetSelection(Context.SelectedIndex == Index);
        }

        public override void UpdatePosition(float position)
        {
            currentPosition = position;

            if (scrollAnimator.isActiveAndEnabled)
            {
                scrollAnimator.Play(AnimatorHash.Scroll, -1, position);
            }

            scrollAnimator.speed = 0;
        }

        private void SetSelection(bool selected)
        {
            if (currentSelection == selected)
            {
                return;
            }

            currentSelection = selected;
            selectAnimator.SetTrigger(selected ? AnimatorHash.In : AnimatorHash.Out);
            updateSelectionTime = Time.time;
        }

        // GameObject が非アクティブになると Animator がリセットされてしまうため
        // 現在位置を保持しておいて OnEnable のタイミングで現在位置を再設定します
        private float currentPosition = 0;

        private void OnEnable() => UpdatePosition(currentPosition);
    }
}
