#region

using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Math = System.Math;
using Object = Java.Lang.Object;

#endregion

namespace TwoWayView.Core
{
	public enum Direction
	{
		Start,
		End
	}

	public abstract class TwoWayLayoutManager : RecyclerView.LayoutManager
	{
		private readonly Context _context;

		private bool _isVertical = true;
		private int _layoutEnd;

		private int _layoutStart;

		private SavedState _pendingSavedState;
		private int _pendingScrollOffset;
		private int _pendingScrollPosition = RecyclerView.NoPosition;
		private RecyclerView _recyclerView;

		protected TwoWayLayoutManager(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
			
		}

		public TwoWayLayoutManager(Context context, IAttributeSet attrs) : this(context, attrs, 0)
		{
		}

		public TwoWayLayoutManager(Context context, IAttributeSet attrs, int defStyle)
		{
			_context = context;
			var a =
				context.ObtainStyledAttributes(attrs, Resource.Styleable.twowayview_TwoWayLayoutManager, defStyle, 0);

			var indexCount = a.IndexCount;
			for (var i = 0; i < indexCount; i++)
			{
				var attr = a.GetIndex(i);

				if (attr == Resource.Styleable.twowayview_TwoWayLayoutManager_android_orientation)
				{
					var orientation = a.GetInt(attr, -1);
					if (orientation >= 0)
						SetOrientation((Orientation) orientation);
				}
			}

			a.Recycle();
		}

		public TwoWayLayoutManager(Orientation orientation)
		{
			_isVertical = orientation == Orientation.Vertical;
		}

		private int getTotalSpace()
		{
			if (_isVertical)
				return Height - PaddingBottom - PaddingTop;
			return Width - PaddingRight - PaddingLeft;
		}

		protected int GetStartWithPadding()
		{
			return _isVertical ? PaddingTop : PaddingLeft;
		}

		protected int GetEndWithPadding()
		{
			if (_isVertical)
				return Height - PaddingBottom;
			return Width - PaddingRight;
		}

		protected int GetChildStart(View child)
		{
			return _isVertical ? GetDecoratedTop(child) : GetDecoratedLeft(child);
		}

		protected int GetChildEnd(View child)
		{
			return _isVertical ? GetDecoratedBottom(child) : GetDecoratedRight(child);
		}

		protected RecyclerView.Adapter GetAdapter()
		{
			return _recyclerView?.GetAdapter();
		}

		private void OffsetChildren(int offset)
		{
			if (_isVertical)
				OffsetChildrenVertical(offset);
			else
				OffsetChildrenHorizontal(offset);

			_layoutStart += offset;
			_layoutEnd += offset;
		}

		private void RecycleChildrenOutOfBounds(Direction direction, RecyclerView.Recycler recycler)
		{
			if (direction == Direction.End)
				RecycleChildrenFromStart(direction, recycler);
			else
				RecycleChildrenFromEnd(direction, recycler);
		}

		private void RecycleChildrenFromStart(Direction direction, RecyclerView.Recycler recycler)
		{
			var childCount = ChildCount;
			var childrenStart = GetStartWithPadding();

			var detachedCount = 0;
			for (var i = 0; i < childCount; i++)
			{
				var child = GetChildAt(i);
				var childEnd = GetChildEnd(child);

				if (childEnd >= childrenStart)
					break;

				detachedCount++;

				DetachChild(child, direction);
			}

			while (--detachedCount >= 0)
			{
				var child = GetChildAt(0);
				RemoveAndRecycleView(child, recycler);
				UpdateLayoutEdgesFromRemovedChild(child, direction);
			}
		}

		private void RecycleChildrenFromEnd(Direction direction, RecyclerView.Recycler recycler)
		{
			var childrenEnd = GetEndWithPadding();
			var childCount = ChildCount;

			var firstDetachedPos = 0;
			var detachedCount = 0;
			for (var i = childCount - 1; i >= 0; i--)
			{
				var child = GetChildAt(i);
				var childStart = GetChildStart(child);

				if (childStart <= childrenEnd)
					break;

				firstDetachedPos = i;
				detachedCount++;

				DetachChild(child, direction);
			}

			while (--detachedCount >= 0)
			{
				var child = GetChildAt(firstDetachedPos);
				RemoveAndRecycleViewAt(firstDetachedPos, recycler);
				UpdateLayoutEdgesFromRemovedChild(child, direction);
			}
		}

		private int ScrollBy(int delta, RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			var childCount = ChildCount;
			if (childCount == 0 || delta == 0)
				return 0;

			var start = GetStartWithPadding();
			var end = GetEndWithPadding();
			var firstPosition = GetFirstVisiblePosition();

			var totalSpace = getTotalSpace();
			if (delta < 0)
				delta = Math.Max(-(totalSpace - 1), delta);
			else
				delta = Math.Min(totalSpace - 1, delta);
			var cannotScrollBackward = firstPosition == 0 &&
			                           _layoutStart >= start && delta <= 0;
			var cannotScrollForward = firstPosition + childCount == state.ItemCount &&
			                          _layoutEnd <= end && delta >= 0;

			if (cannotScrollForward || cannotScrollBackward)
				return 0;

			OffsetChildren(-delta);

			var direction = delta > 0 ? Direction.End : Direction.Start;
			RecycleChildrenOutOfBounds(direction, recycler);

			var absDelta = Math.Abs(delta);
			if (CanAddMoreViews(Direction.Start, start - absDelta) ||
			    CanAddMoreViews(Direction.End, end + absDelta))
				FillGap(direction, recycler, state);

			return delta;
		}

		private void FillGap(Direction direction, RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			var childCount = ChildCount;
			var extraSpace = GetExtraLayoutSpace(state);
			var firstPosition = GetFirstVisiblePosition();

			if (direction == Direction.End)
			{
				FillAfter(firstPosition + childCount, recycler, state, extraSpace);
				CorrectTooHigh(childCount, recycler, state);
			}
			else
			{
				FillBefore(firstPosition - 1, recycler, extraSpace);
				CorrectTooLow(childCount, recycler, state);
			}
		}

		private void FillBefore(int pos, RecyclerView.Recycler recycler)
		{
			FillBefore(pos, recycler, 0);
		}

		private void FillBefore(int position, RecyclerView.Recycler recycler, int extraSpace)
		{
			var limit = GetStartWithPadding() - extraSpace;

			while (CanAddMoreViews(Direction.Start, limit) && position >= 0)
			{
				MakeAndAddView(position, Direction.Start, recycler);
				position--;
			}
		}

		private void FillAfter(int pos, RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			FillAfter(pos, recycler, state, 0);
		}

		private void FillAfter(int position, RecyclerView.Recycler recycler, RecyclerView.State state, int extraSpace)
		{
			var limit = GetEndWithPadding() + extraSpace;

			var itemCount = state.ItemCount;
			while (CanAddMoreViews(Direction.End, limit) && position < itemCount)
			{
				MakeAndAddView(position, Direction.End, recycler);
				position++;
			}
		}

		private void FillSpecific(int position, RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			if (state.ItemCount == 0)
				return;

			MakeAndAddView(position, Direction.End, recycler);

			int extraSpaceBefore;
			int extraSpaceAfter;

			var extraSpace = GetExtraLayoutSpace(state);
			if (state.TargetScrollPosition < position)
			{
				extraSpaceAfter = 0;
				extraSpaceBefore = extraSpace;
			}
			else
			{
				extraSpaceAfter = extraSpace;
				extraSpaceBefore = 0;
			}

			FillBefore(position - 1, recycler, extraSpaceBefore);

			// This will correct for the top of the first view not
			// touching the top of the parent.
			AdjustViewsStartOrEnd();

			FillAfter(position + 1, recycler, state, extraSpaceAfter);
			CorrectTooHigh(ChildCount, recycler, state);
		}

		private void CorrectTooHigh(int childCount, RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			// First see if the last item is visible. If it is not, it is OK for the
			// top of the list to be pushed up.
			var lastPosition = GetLastVisiblePosition();
			if (lastPosition != state.ItemCount - 1 || childCount == 0)
				return;

			// This is bottom of our drawable area.
			var start = GetStartWithPadding();
			var end = GetEndWithPadding();
			var firstPosition = GetFirstVisiblePosition();

			// This is how far the end edge of the last view is from the end of the
			// drawable area.
			var endOffset = end - _layoutEnd;

			// Make sure we are 1) Too high, and 2) Either there are more rows above the
			// first row or the first row is scrolled off the top of the drawable area
			if (endOffset > 0 && (firstPosition > 0 || _layoutStart < start))
			{
				if (firstPosition == 0)
					endOffset = Math.Min(endOffset, start - _layoutStart);

				// Move everything down
				OffsetChildren(endOffset);

				if (firstPosition > 0)
				{
					// Fill the gap that was opened above first position with more
					// children, if possible.
					FillBefore(firstPosition - 1, recycler);

					// Close up the remaining gap.
					AdjustViewsStartOrEnd();
				}
			}
		}

		private void CorrectTooLow(int childCount, RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			// First see if the first item is visible. If it is not, it is OK for the
			// end of the list to be pushed forward.
			var firstPosition = GetFirstVisiblePosition();
			if (firstPosition != 0 || childCount == 0)
				return;

			var start = GetStartWithPadding();
			var end = GetEndWithPadding();
			var itemCount = state.ItemCount;
			var lastPosition = GetLastVisiblePosition();

			// This is how far the start edge of the first view is from the start of the
			// drawable area.
			var startOffset = _layoutStart - start;

			// Make sure we are 1) Too low, and 2) Either there are more columns/rows below the
			// last column/row or the last column/row is scrolled off the end of the
			// drawable area.
			if (startOffset > 0)
				if (lastPosition < itemCount - 1 || _layoutEnd > end)
				{
					if (lastPosition == itemCount - 1)
						startOffset = Math.Min(startOffset, _layoutEnd - end);

					// Move everything up.
					OffsetChildren(-startOffset);

					if (lastPosition < itemCount - 1)
					{
						// Fill the gap that was opened below the last position with more
						// children, if possible.
						FillAfter(lastPosition + 1, recycler, state);

						// Close up the remaining gap.
						AdjustViewsStartOrEnd();
					}
				}
				else if (lastPosition == itemCount - 1)
				{
					AdjustViewsStartOrEnd();
				}
		}

		private void AdjustViewsStartOrEnd()
		{
			if (ChildCount == 0)
				return;

			var delta = _layoutStart - GetStartWithPadding();
			if (delta < 0)
				delta = 0;

			if (delta != 0)
				OffsetChildren(-delta);
		}

		private static View FindNextScrapView(IList<RecyclerView.ViewHolder> scrapList, Direction direction,
			int position)
		{
			var scrapCount = scrapList.Count;

			RecyclerView.ViewHolder closest = null;
			var closestDistance = int.MaxValue;

			for (var i = 0; i < scrapCount; i++)
			{
				var holder = scrapList[i];

				var distance = holder.AdapterPosition - position;
				if (distance < 0 && direction == Direction.End ||
				    distance > 0 && direction == Direction.Start)
					continue;

				var absDistance = Math.Abs(distance);
				if (absDistance < closestDistance)
				{
					closest = holder;
					closestDistance = absDistance;

					if (distance == 0)
						break;
				}
			}

			if (closest != null)
				return closest.ItemView;

			return null;
		}

		private void FillFromScrapList(IList<RecyclerView.ViewHolder> scrapList, Direction direction)
		{
			var firstPosition = GetFirstVisiblePosition();

			int position;
			if (direction == Direction.End)
				position = firstPosition + ChildCount;
			else
				position = firstPosition - 1;

			View scrapChild;
			while ((scrapChild = FindNextScrapView(scrapList, direction, position)) != null)
			{
				SetupChild(scrapChild, direction);
				position += direction == Direction.End ? 1 : -1;
			}
		}

		private void SetupChild(View child, Direction direction)
		{
			var itemSelection = ItemSelectionSupport.From(_recyclerView);
			if (itemSelection != null)
			{
				var position = GetPosition(child);
				itemSelection.setViewChecked(child, itemSelection.IsItemChecked(position));
			}

			MeasureChild(child, direction);
			LayoutChild(child, direction);
		}

		private View MakeAndAddView(int position, Direction direction, RecyclerView.Recycler recycler)
		{
			var child = recycler.GetViewForPosition(position);
			var isItemRemoved = ((RecyclerView.LayoutParams) child.LayoutParameters).IsItemRemoved;

			if (!isItemRemoved)
				AddView(child, direction == Direction.End ? -1 : 0);

			SetupChild(child, direction);

			if (!isItemRemoved)
				UpdateLayoutEdgesFromNewChild(child);

			return child;
		}

		private void HandleUpdate()
		{
			// Refresh state by requesting layout without changing the
			// first visible position. This will ensure the layout will
			// sync with the adapter changes.
			var firstPosition = GetFirstVisiblePosition();
			var firstChild = FindViewByPosition(firstPosition);
			if (firstChild != null)
				SetPendingScrollPositionWithOffset(firstPosition, GetChildStart(firstChild));
			else
				SetPendingScrollPositionWithOffset(RecyclerView.NoPosition, 0);
		}

		private void UpdateLayoutEdgesFromNewChild(View newChild)
		{
			var childStart = GetChildStart(newChild);
			if (childStart < _layoutStart)
				_layoutStart = childStart;

			var childEnd = GetChildEnd(newChild);
			if (childEnd > _layoutEnd)
				_layoutEnd = childEnd;
		}

		private void UpdateLayoutEdgesFromRemovedChild(View removedChild, Direction direction)
		{
			var childCount = ChildCount;
			if (childCount == 0)
			{
				ResetLayoutEdges();
				return;
			}

			var removedChildStart = GetChildStart(removedChild);
			var removedChildEnd = GetChildEnd(removedChild);

			if (removedChildStart > _layoutStart && removedChildEnd < _layoutEnd)
				return;

			int index;
			int limit;
			if (direction == Direction.End)
			{
				// Scrolling towards the end of the layout, child view being
				// removed from the start.
				_layoutStart = int.MaxValue;
				index = 0;
				limit = removedChildEnd;
			}
			else
			{
				// Scrolling towards the start of the layout, child view being
				// removed from the end.
				_layoutEnd = int.MaxValue;
				index = childCount - 1;
				limit = removedChildStart;
			}

			while (index >= 0 && index <= childCount - 1)
			{
				var child = GetChildAt(index);

				if (direction == Direction.End)
				{
					var childStart = GetChildStart(child);
					if (childStart < _layoutStart)
						_layoutStart = childStart;

					// Checked enough child views to update the minimum
					// layout start edge, stop.
					if (childStart >= limit)
						break;

					index++;
				}
				else
				{
					var childEnd = GetChildEnd(child);
					if (childEnd > _layoutEnd)
						_layoutEnd = childEnd;

					// Checked enough child views to update the minimum
					// layout end edge, stop.
					if (childEnd <= limit)
						break;

					index--;
				}
			}
		}

		private void ResetLayoutEdges()
		{
			_layoutStart = GetStartWithPadding();
			_layoutEnd = _layoutStart;
		}

		protected int GetExtraLayoutSpace(RecyclerView.State state)
		{
			if (state.HasTargetScrollPosition)
				return getTotalSpace();
			return 0;
		}

		private Bundle GetPendingItemSelectionState()
		{
			if (_pendingSavedState != null)
				return _pendingSavedState.ItemSelectionState;

			return null;
		}

		protected void SetPendingScrollPositionWithOffset(int position, int offset)
		{
			_pendingScrollPosition = position;
			_pendingScrollOffset = offset;
		}

		protected int GetPendingScrollPosition()
		{
			if (_pendingSavedState != null)
				return _pendingSavedState.anchorItemPosition;

			return _pendingScrollPosition;
		}

		protected int GetPendingScrollOffset()
		{
			if (_pendingSavedState != null)
				return 0;

			return _pendingScrollOffset;
		}

		protected int GetAnchorItemPosition(RecyclerView.State state)
		{
			var itemCount = state.ItemCount;

			var pendingPosition = GetPendingScrollPosition();
			if (pendingPosition != RecyclerView.NoPosition)
				if (pendingPosition < 0 || pendingPosition >= itemCount)
					pendingPosition = RecyclerView.NoPosition;

			if (pendingPosition != RecyclerView.NoPosition)
				return pendingPosition;
			if (ChildCount > 0)
				return FindFirstValidChildPosition(itemCount);
			return 0;
		}

		private int FindFirstValidChildPosition(int itemCount)
		{
			var childCount = ChildCount;
			for (var i = 0; i < childCount; i++)
			{
				var view = GetChildAt(i);
				var position = GetPosition(view);
				if (position >= 0 && position < itemCount)
					return position;
			}

			return 0;
		}

		public override int GetDecoratedMeasuredWidth(View child)
		{
			var lp = (ViewGroup.MarginLayoutParams) child.LayoutParameters;
			return base.GetDecoratedMeasuredWidth(child) + lp.LeftMargin + lp.RightMargin;
		}

		public override int GetDecoratedMeasuredHeight(View child)
		{
			var lp = (ViewGroup.MarginLayoutParams) child.LayoutParameters;
			return base.GetDecoratedMeasuredHeight(child) + lp.TopMargin + lp.BottomMargin;
		}

		public override int GetDecoratedLeft(View child)
		{
			var lp = (ViewGroup.MarginLayoutParams) child.LayoutParameters;
			return base.GetDecoratedLeft(child) - lp.LeftMargin;
		}

		public override int GetDecoratedTop(View child)
		{
			var lp = (ViewGroup.MarginLayoutParams) child.LayoutParameters;
			return base.GetDecoratedTop(child) - lp.TopMargin;
		}

		public override int GetDecoratedRight(View child)
		{
			var lp = (ViewGroup.MarginLayoutParams) child.LayoutParameters;
			return base.GetDecoratedRight(child) + lp.RightMargin;
		}

		public override int GetDecoratedBottom(View child)
		{
			var lp = (ViewGroup.MarginLayoutParams) child.LayoutParameters;
			return base.GetDecoratedBottom(child) + lp.BottomMargin;
		}

		public override void LayoutDecorated(View child, int left, int top, int right, int bottom)
		{
			var lp = (ViewGroup.MarginLayoutParams) child.LayoutParameters;
			base.LayoutDecorated(child, left + lp.LeftMargin, top + lp.TopMargin,
				right - lp.RightMargin, bottom - lp.BottomMargin);
		}

		public override void OnAttachedToWindow(RecyclerView view)
		{
			base.OnAttachedToWindow(view);
			_recyclerView = view;
		}

		public override void OnDetachedFromWindow(RecyclerView view, RecyclerView.Recycler recycler)
		{
			base.OnDetachedFromWindow(view, recycler);
			_recyclerView = null;
		}

		public override void OnAdapterChanged(RecyclerView.Adapter oldAdapter, RecyclerView.Adapter newAdapter)
		{
			base.OnAdapterChanged(oldAdapter, newAdapter);

			var itemSelectionSupport = ItemSelectionSupport.From(_recyclerView);
			if (oldAdapter != null && itemSelectionSupport != null)
				itemSelectionSupport.ClearChoices();
		}

		public override void OnLayoutChildren(RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			var itemSelection = ItemSelectionSupport.From(_recyclerView);
			if (itemSelection != null)
			{
				var itemSelectionState = GetPendingItemSelectionState();
				if (itemSelectionState != null)
					itemSelection.OnRestoreInstanceState(itemSelectionState);

				if (state.DidStructureChange())
					itemSelection.OnAdapterDataChanged();
			}

			var anchorItemPosition = GetAnchorItemPosition(state);
			DetachAndScrapAttachedViews(recycler);
			FillSpecific(anchorItemPosition, recycler, state);

			onLayoutScrapList(recycler, state);

			SetPendingScrollPositionWithOffset(RecyclerView.NoPosition, 0);
			_pendingSavedState = null;
		}

		protected virtual void onLayoutScrapList(RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			var childCount = ChildCount;
			if (childCount == 0 || state.IsPreLayout || !SupportsPredictiveItemAnimations())
				return;

			var scrapList = recycler.ScrapList;
			FillFromScrapList(scrapList, Direction.Start);
			FillFromScrapList(scrapList, Direction.End);
		}

		protected virtual void DetachChild(View child, Direction direction)
		{
			// Do nothing by default.
		}


		public override void OnItemsAdded(RecyclerView recyclerView, int positionStart, int itemCount)
		{
			HandleUpdate();
		}

		public override void OnItemsRemoved(RecyclerView recyclerView, int positionStart, int itemCount)
		{
			HandleUpdate();
		}

		public override void OnItemsUpdated(RecyclerView recyclerView, int positionStart, int itemCount)
		{
			HandleUpdate();
		}

		public override void OnItemsMoved(RecyclerView recyclerView, int from, int to, int itemCount)
		{
			HandleUpdate();
		}


		public override void OnItemsChanged(RecyclerView recyclerView)
		{
			HandleUpdate();
		}

		public override RecyclerView.LayoutParams GenerateDefaultLayoutParams()
		{
			if (_isVertical)
				return new RecyclerView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
			return new RecyclerView.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
		}

		public override bool SupportsPredictiveItemAnimations()
		{
			return true;
		}

		public override int ScrollHorizontallyBy(int dx, RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			if (_isVertical)
				return 0;

			return ScrollBy(dx, recycler, state);
		}

		public override int ScrollVerticallyBy(int dy, RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			if (!_isVertical)
				return 0;

			return ScrollBy(dy, recycler, state);
		}

		public override bool CanScrollHorizontally()
		{
			return !_isVertical;
		}

		public override bool CanScrollVertically()
		{
			return _isVertical;
		}

		public override void ScrollToPosition(int position)
		{
			ScrollToPositionWithOffset(position, 0);
		}

		public void ScrollToPositionWithOffset(int position, int offset)
		{
			SetPendingScrollPositionWithOffset(position, offset);
			RequestLayout();
		}

		public override void SmoothScrollToPosition(RecyclerView recyclerView, RecyclerView.State state, int position)
		{
			LinearSmoothScroller scroller = new MyLinearSmoothScroller(_context, GetFirstVisiblePosition, () => _isVertical);

			scroller.TargetPosition = position;

			StartSmoothScroll(scroller);
		}

		public override int ComputeHorizontalScrollOffset(RecyclerView.State state)
		{
			if (ChildCount == 0)
				return 0;

			return GetFirstVisiblePosition();
		}

		public override int ComputeVerticalScrollOffset(RecyclerView.State state)
		{
			if (ChildCount == 0)
				return 0;

			return GetFirstVisiblePosition();
		}

		public override int ComputeHorizontalScrollExtent(RecyclerView.State state)
		{
			return ChildCount;
		}

		public override int ComputeVerticalScrollExtent(RecyclerView.State state)
		{
			return ChildCount;
		}

		public override int ComputeHorizontalScrollRange(RecyclerView.State state)
		{
			return state.ItemCount;
		}

		public override int ComputeVerticalScrollRange(RecyclerView.State state)
		{
			return state.ItemCount;
		}


		public override IParcelable OnSaveInstanceState()
		{
			var state = new SavedState(SavedState.EMPTY_STATE);

			var anchorItemPosition = GetPendingScrollPosition();
			if (anchorItemPosition == RecyclerView.NoPosition)
				anchorItemPosition = GetFirstVisiblePosition();
			state.anchorItemPosition = anchorItemPosition;

			var itemSelection = ItemSelectionSupport.From(_recyclerView);
			if (itemSelection != null)
				state.ItemSelectionState = itemSelection.OnSaveInstanceState();
			else
				state.ItemSelectionState = Bundle.Empty;

			return state;
		}


		public override void OnRestoreInstanceState(IParcelable state)
		{
			_pendingSavedState = (SavedState) state;
			RequestLayout();
		}

		public Orientation GetOrientation()
		{
			return _isVertical ? Orientation.Vertical : Orientation.Horizontal;
		}

		public void SetOrientation(Orientation orientation)
		{
			var isVertical = orientation == Orientation.Vertical;
			if (_isVertical == isVertical)
				return;

			_isVertical = isVertical;
			RequestLayout();
		}

		public int GetFirstVisiblePosition()
		{
			if (ChildCount == 0)
				return 0;

			return GetPosition(GetChildAt(0));
		}

		public int GetLastVisiblePosition()
		{
			var childCount = ChildCount;
			if (childCount == 0)
				return 0;

			return GetPosition(GetChildAt(childCount - 1));
		}

		protected abstract void MeasureChild(View child, Direction direction);
		protected abstract void LayoutChild(View child, Direction direction);

		protected abstract bool CanAddMoreViews(Direction direction, int limit);

		public class CheckedIdStates : LongSparseArray, IParcelable
		{
			public CheckedIdStates()
			{
			}

			public CheckedIdStates(Parcel @in)
			{
				var size = @in.ReadInt();
				if (size > 0)
					for (var i = 0; i < size; i++)
					{
						var key = @in.ReadLong();
						var value = @in.ReadInt();
						Put(key, value);
					}
			}

			public int DescribeContents()
			{
				return 0;
			}

			public void WriteToParcel(Parcel parcel, [GeneratedEnum] ParcelableWriteFlags flags)
			{
				var size = Size();
				parcel.WriteInt(size);

				for (var i = 0; i < size; i++)
				{
					parcel.WriteLong(KeyAt(i));
					parcel.WriteInt((int) ValueAt(i));
				}
			}

			public CheckedIdStates[] NewArray(int size)
			{
				return new CheckedIdStates[size];
			}
		}

		public class CheckedStates : SparseBooleanArray, IParcelable
		{
			private static readonly int FALSE = 0;
			private static readonly int TRUE = 1;

			public CheckedStates()
			{
			}

			public CheckedStates(Parcel @in)
			{
				var size = @in.ReadInt();
				if (size > 0)
					for (var i = 0; i < size; i++)
					{
						var key = @in.ReadInt();
						var value = @in.ReadInt() == TRUE;
						Put(key, value);
					}
			}

			public int DescribeContents()
			{
				return 0;
			}

			public void WriteToParcel(Parcel parcel, ParcelableWriteFlags flags)
			{
				var size = Size();
				parcel.WriteInt(size);

				for (var i = 0; i < size; i++)
				{
					parcel.WriteInt(KeyAt(i));
					parcel.WriteInt(ValueAt(i) ? TRUE : FALSE);
				}
			}

			public CheckedStates[] NewArray(int size)
			{
				return new CheckedStates[size];
			}
		}

		protected class SavedState : Object, IParcelable
		{
			public static readonly SavedState EMPTY_STATE = new SavedState();

			private readonly IParcelable _superState;
			public int anchorItemPosition;
			public Bundle ItemSelectionState;


			protected SavedState()
			{
			}

			public SavedState(IParcelable superState)
			{
				if (superState == null)
					throw new IllegalArgumentException("_superState must not be null");

				_superState = superState != EMPTY_STATE ? superState : null;
			}

			public SavedState(Parcel In)
			{
				_superState = EMPTY_STATE;
				anchorItemPosition = In.ReadInt();
				ItemSelectionState = (Bundle) In.ReadParcelable(Class.ClassLoader);
			}


			public int DescribeContents()
			{
				return 0;
			}

			public virtual void WriteToParcel(Parcel @out, ParcelableWriteFlags flags)
			{
				@out.WriteInt(anchorItemPosition);
				@out.WriteParcelable(ItemSelectionState, flags);
			}

			public IParcelable GetSuperState()
			{
				return _superState;
			}
		}

		private class MyLinearSmoothScroller : LinearSmoothScroller
		{
			private readonly Func<int> _firstVisiblePositionFunc;
			private readonly Func<bool> _isVerticalFunc;

			public MyLinearSmoothScroller(Context context, Func<int> firstVisiblePositionFunc,
				Func<bool> isVerticalFunc) : base(context)
			{
				_firstVisiblePositionFunc = firstVisiblePositionFunc;
				_isVerticalFunc = isVerticalFunc;
			}

			protected override int VerticalSnapPreference => SnapToStart;

			protected override int HorizontalSnapPreference => SnapToStart;

			public override PointF ComputeScrollVectorForPosition(int targetPosition)
			{
				if (ChildCount == 0)
					return null;

				var direction = TargetPosition < _firstVisiblePositionFunc() ? -1 : 1;
				if (_isVerticalFunc())
					return new PointF(0, direction);
				return new PointF(direction, 0);
			}
		}
	}
}