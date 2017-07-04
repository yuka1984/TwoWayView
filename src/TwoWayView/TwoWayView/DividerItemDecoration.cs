#region

using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;

#endregion

namespace TwoWayView.Layout
{
	public class DividerItemDecoration : RecyclerView.ItemDecoration
	{
		private readonly Drawable _horizontalDivider;

		private readonly ItemSpacingOffsets _itemSpacing;

		private readonly Drawable _verticalDivider;

		public DividerItemDecoration(Context context, IAttributeSet attrs) : this(context, attrs, 0)
		{
		}

		public DividerItemDecoration(Context context, IAttributeSet attrs, int defStyle)
		{
			var a =
				context.ObtainStyledAttributes(attrs, Resource.Styleable.twowayview_DividerItemDecoration, defStyle, 0);

			var divider = a.GetDrawable(Resource.Styleable.twowayview_DividerItemDecoration_android_divider);
			if (divider != null)
			{
				_verticalDivider = _horizontalDivider = divider;
			}
			else
			{
				_verticalDivider = a.GetDrawable(Resource.Styleable.twowayview_DividerItemDecoration_twowayview_verticalDivider);
				_horizontalDivider = a.GetDrawable(Resource.Styleable
					.twowayview_DividerItemDecoration_twowayview_horizontalDivider);
			}

			a.Recycle();

			_itemSpacing = CreateSpacing(_verticalDivider, _horizontalDivider);
		}

		public DividerItemDecoration(Drawable divider) : this(divider, divider)
		{
		}

		public DividerItemDecoration(Drawable verticalDivider, Drawable horizontalDivider)
		{
			_verticalDivider = verticalDivider;
			_horizontalDivider = horizontalDivider;
			_itemSpacing = CreateSpacing(_verticalDivider, _horizontalDivider);
		}

		private static ItemSpacingOffsets CreateSpacing(Drawable verticalDivider,
			Drawable horizontalDivider)
		{
			int verticalSpacing;
			if (horizontalDivider != null)
				verticalSpacing = horizontalDivider.IntrinsicHeight;
			else
				verticalSpacing = 0;

			int horizontalSpacing;
			if (verticalDivider != null)
				horizontalSpacing = verticalDivider.IntrinsicWidth;
			else
				horizontalSpacing = 0;

			var spacing = new ItemSpacingOffsets(verticalSpacing, horizontalSpacing);
			spacing.SetAddSpacingAtEnd(true);

			return spacing;
		}

		public override void OnDrawOver(Canvas c, RecyclerView parent)
		{
			var lm = (BaseLayoutManager) parent.GetLayoutManager();

			var rightWithPadding = parent.Width - parent.PaddingRight;
			var bottomWithPadding = parent.Height - parent.PaddingBottom;

			var childCount = parent.ChildCount;
			for (var i = 0; i < childCount; i++)
			{
				var child = parent.GetChildAt(i);

				var childLeft = lm.GetDecoratedLeft(child);
				var childTop = lm.GetDecoratedTop(child);
				var childRight = lm.GetDecoratedRight(child);
				var childBottom = lm.GetDecoratedBottom(child);

				var lp = (ViewGroup.MarginLayoutParams) child.LayoutParameters;

				var bottomOffset = childBottom - child.Bottom - lp.BottomMargin;
				if (bottomOffset > 0 && childBottom < bottomWithPadding)
				{
					var left = childLeft;
					var top = childBottom - bottomOffset;
					var right = childRight;
					var bottom = top + _horizontalDivider.IntrinsicHeight;

					_horizontalDivider.SetBounds(left, top, right, bottom);
					_horizontalDivider.Draw(c);
				}

				var rightOffset = childRight - child.Right - lp.RightMargin;
				if (rightOffset > 0 && childRight < rightWithPadding)
				{
					var left = childRight - rightOffset;
					var top = childTop;
					var right = left + _verticalDivider.IntrinsicWidth;
					var bottom = childBottom;

					_verticalDivider.SetBounds(left, top, right, bottom);
					_verticalDivider.Draw(c);
				}
			}
		}


		public override void GetItemOffsets(Rect outRect, int itemPosition, RecyclerView parent)
		{
			_itemSpacing.GetItemOffsets(outRect, itemPosition, parent);
		}
	}
}