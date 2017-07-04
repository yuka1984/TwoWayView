#region

using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using TwoWayview.Layout;
using TwoWayView.Core;

#endregion

namespace TwoWayView.Layout
{
	public abstract class BaseLayoutManager : TwoWayLayoutManager
	{
		protected readonly Rect ChildFrame = new Rect();

		private ItemEntries _itemEntries;
		private ItemEntries _itemEntriesToRestore;

		private Lanes _lanesToRestore;
		protected readonly Lanes.LaneInfo TempLaneInfo = new Lanes.LaneInfo();
		protected readonly Rect TempRect = new Rect();

		protected BaseLayoutManager(Context context, IAttributeSet attrs) : this(context, attrs, 0)
		{
			
		}

		protected BaseLayoutManager(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
		{
			
		}

		protected BaseLayoutManager(Orientation orientation) : base(orientation)
		{
			
		}

		public Lanes Lanes { get; private set; }

		protected void PushChildFrame(ItemEntry entry, Rect childFrame, int lane, int laneSpan,
			Direction direction)
		{
			var shouldSetMargins = direction == Direction.End &&
			                       entry != null && !entry.hasSpanMargins();

			for (var i = lane; i < lane + laneSpan; i++)
			{
				int spanMargin;
				if (entry != null && direction != Direction.End)
					spanMargin = entry.getSpanMargin(i - lane);
				else
					spanMargin = 0;

				var margin = Lanes.PushChildFrame(childFrame, i, spanMargin, direction);
				if (laneSpan > 1 && shouldSetMargins)
					entry.setSpanMargin(i - lane, margin, laneSpan);
			}
		}

		private void popChildFrame(ItemEntry entry, Rect childFrame, int lane, int laneSpan,
			Direction direction)
		{
			for (var i = lane; i < lane + laneSpan; i++)
			{
				int spanMargin;
				if (entry != null && direction != Direction.End)
					spanMargin = entry.getSpanMargin(i - lane);
				else
					spanMargin = 0;

				Lanes.PopChildFrame(childFrame, i, spanMargin, direction);
			}
		}

		private void GetDecoratedChildFrame(View child, Rect childFrame)
		{
			childFrame.Left = GetDecoratedLeft(child);
			childFrame.Top = GetDecoratedTop(child);
			childFrame.Right = GetDecoratedRight(child);
			childFrame.Bottom = GetDecoratedBottom(child);
		}

		public bool IsVertical() => GetOrientation() == Orientation.Vertical;

		public Lanes GetLanes()
		{
			return Lanes;
		}

		protected void SetItemEntryForPosition(int position, ItemEntry entry)
		{
			_itemEntries?.PutItemEntry(position, entry);
		}

		protected ItemEntry GetItemEntryForPosition(int position)
		{
			return _itemEntries?.GetItemEntry(position);
		}

		private void ClearItemEntries()
		{
			_itemEntries?.Clear();
		}

		private void InvalidateItemLanesAfter(int position)
		{
			_itemEntries?.invalidateItemLanesAfter(position);
		}

		private void OffsetForAddition(int positionStart, int itemCount)
		{
			_itemEntries?.OffsetForAddition(positionStart, itemCount);
		}

		private void OffsetForRemoval(int positionStart, int itemCount)
		{
			_itemEntries?.OffsetForRemoval(positionStart, itemCount);
		}

		private void RequestMoveLayout()
		{
			if (GetPendingScrollPosition() != RecyclerView.NoPosition)
				return;

			var position = GetFirstVisiblePosition();
			var firstChild = FindViewByPosition(position);
			var offset = firstChild != null ? GetChildStart(firstChild) : 0;

			SetPendingScrollPositionWithOffset(position, offset);
		}

		private bool CanUseLanes(Lanes lanes)
		{
			if (lanes == null)
				return false;


			var laneCount = GetLaneCount();
			var laneSize = Lanes.CalculateLaneSize(this, laneCount);

			return lanes.GetOrientation() == GetOrientation() &&
			       lanes.GetCount() == laneCount &&
			       lanes.GetLaneSize() == laneSize;
		}

		private bool EnsureLayoutState()
		{
			var laneCount = GetLaneCount();
			if (laneCount == 0 || Width == 0 || Height == 0)
				return false;

			var oldLanes = Lanes;
			Lanes = new Lanes(this, laneCount);

			RequestMoveLayout();

			if (_itemEntries == null)
				_itemEntries = new ItemEntries();

			if (oldLanes != null && oldLanes.GetOrientation() == Lanes.GetOrientation() &&
			    oldLanes.GetLaneSize() == Lanes.GetLaneSize())
				InvalidateItemLanesAfter(0);
			else
				_itemEntries.Clear();

			return true;
		}

		private void HandleUpdate(int positionStart, int itemCountOrToPosition, UpdateOp cmd)
		{
			InvalidateItemLanesAfter(positionStart);

			switch (cmd)
			{
				case UpdateOp.ADD:
					OffsetForAddition(positionStart, itemCountOrToPosition);
					break;

				case UpdateOp.REMOVE:
					OffsetForRemoval(positionStart, itemCountOrToPosition);
					break;

				case UpdateOp.MOVE:
					OffsetForRemoval(positionStart, 1);
					OffsetForAddition(itemCountOrToPosition, 1);
					break;
			}

			if (positionStart + itemCountOrToPosition <= GetFirstVisiblePosition())
				return;

			if (positionStart <= GetLastVisiblePosition())
				RequestLayout();
		}

		public override void OffsetChildrenHorizontal(int offset)
		{
			if (!IsVertical())
				Lanes.Offset(offset);

			base.OffsetChildrenHorizontal(offset);
		}

		public override void OffsetChildrenVertical(int offset)
		{
			base.OffsetChildrenVertical(offset);

			if (IsVertical())
				Lanes.Offset(offset);
		}

		public override void OnLayoutChildren(RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			var restoringLanes = _lanesToRestore != null;
			if (restoringLanes)
			{
				Lanes = _lanesToRestore;
				_itemEntries = _itemEntriesToRestore;

				_lanesToRestore = null;
				_itemEntriesToRestore = null;
			}

			var refreshingLanes = EnsureLayoutState();

			// Still not able to create lanes, nothing we can do here,
			// just bail for now.
			if (Lanes == null)
				return;

			var itemCount = state.ItemCount;

			if (_itemEntries != null)
				_itemEntries.SetAdapterSize(itemCount);

			var anchorItemPosition = GetAnchorItemPosition(state);


			// Only move layout if we're not restoring a layout state.
			if (anchorItemPosition > 0 && (refreshingLanes || !restoringLanes))
				MoveLayoutToPosition(anchorItemPosition, GetPendingScrollOffset(), recycler, state);

			Lanes.Reset(Direction.Start);
			base.OnLayoutChildren(recycler, state);
		}

		protected override void onLayoutScrapList(RecyclerView.Recycler recycler, RecyclerView.State state)
		{
			Lanes.Save();
			base.onLayoutScrapList(recycler, state);
			Lanes.Restore();
		}

		public override void OnItemsAdded(RecyclerView recyclerView, int positionStart, int itemCount)
		{
			HandleUpdate(positionStart, itemCount, UpdateOp.ADD);
			base.OnItemsAdded(recyclerView, positionStart, itemCount);
		}

		public override void OnItemsRemoved(RecyclerView recyclerView, int positionStart, int itemCount)
		{
			HandleUpdate(positionStart, itemCount, UpdateOp.REMOVE);
			base.OnItemsRemoved(recyclerView, positionStart, itemCount);
		}

		public override void OnItemsUpdated(RecyclerView recyclerView, int positionStart, int itemCount)
		{
			HandleUpdate(positionStart, itemCount, UpdateOp.UPDATE);
			base.OnItemsUpdated(recyclerView, positionStart, itemCount);
		}

		public override void OnItemsMoved(RecyclerView recyclerView, int from, int to, int itemCount)
		{
			HandleUpdate(from, to, UpdateOp.MOVE);
			base.OnItemsMoved(recyclerView, from, to, itemCount);
		}

		public override void OnItemsChanged(RecyclerView recyclerView)
		{
			ClearItemEntries();
			base.OnItemsChanged(recyclerView);
		}

		public override IParcelable OnSaveInstanceState()
		{
			var superState = base.OnSaveInstanceState();
			var state = new LanedSavedState(superState);

			var laneCount = Lanes?.GetCount() ?? 0;
			state.lanes = new Rect[laneCount];
			for (var i = 0; i < laneCount; i++)
			{
				var laneRect = new Rect();
				Lanes?.GetLane(i, laneRect);
				state.lanes[i] = laneRect;
			}

			state.orientation = GetOrientation();
			state.laneSize = Lanes?.GetLaneSize() ?? 0;
			state.itemEntries = _itemEntries;

			return state;
		}


		public override void OnRestoreInstanceState(IParcelable state)
		{
			var ss = (LanedSavedState) state;

			if (ss.lanes != null && ss.laneSize > 0)
			{
				_lanesToRestore = new Lanes(this, ss.orientation, ss.lanes, ss.laneSize);
				_itemEntriesToRestore = ss.itemEntries;
			}

			base.OnRestoreInstanceState(ss.GetSuperState());
		}


		protected override bool CanAddMoreViews(Direction direction, int limit)
		{
			if (direction == Direction.Start)
				return Lanes.GetInnerStart() > limit;
			return Lanes.GetInnerEnd() < limit;
		}

		private int GetWidthUsed(View child)
		{
			if (!IsVertical())
				return 0;

			var size = GetLanes().GetLaneSize() * GetLaneSpanForChild(child);
			return Width - PaddingLeft - PaddingRight - size;
		}

		private int GetHeightUsed(View child)
		{
			if (IsVertical())
				return 0;

			var size = GetLanes().GetLaneSize() * GetLaneSpanForChild(child);
			return Height - PaddingTop - PaddingBottom - size;
		}

		protected virtual void MeasureChildWithMargins(View child)
		{
			MeasureChildWithMargins(child, GetWidthUsed(child), GetHeightUsed(child));
		}

		protected override void MeasureChild(View child, Direction direction)
		{
			CacheChildLaneAndSpan(child, direction);
			MeasureChildWithMargins(child);
		}

		protected override void LayoutChild(View child, Direction direction)
		{
			GetLaneForChild(TempLaneInfo, child, direction);

			Lanes.GetChildFrame(ChildFrame, GetDecoratedMeasuredWidth(child),
				GetDecoratedMeasuredHeight(child), TempLaneInfo, direction);
			var entry = CacheChildFrame(child, ChildFrame);

			LayoutDecorated(child, ChildFrame.Left, ChildFrame.Top, ChildFrame.Right,
				ChildFrame.Bottom);

			var lp = (RecyclerView.LayoutParams) child.LayoutParameters;
			if (!lp.IsItemRemoved)
				PushChildFrame(entry, ChildFrame, TempLaneInfo.StartLane,
					GetLaneSpanForChild(child), direction);
		}


		protected override void DetachChild(View child, Direction direction)
		{
			var position = GetPosition(child);
			GetLaneForPosition(TempLaneInfo, position, direction);
			GetDecoratedChildFrame(child, ChildFrame);


			popChildFrame(GetItemEntryForPosition(position), ChildFrame, TempLaneInfo.StartLane,
				GetLaneSpanForChild(child), direction);
		}

		protected virtual void GetLaneForChild(Lanes.LaneInfo outInfo, View child, Direction direction)
		{
			GetLaneForPosition(outInfo, GetPosition(child), direction);
		}

		public virtual int GetLaneSpanForChild(View child)
		{
			return 1;
		}

		public virtual int GetLaneSpanForPosition(int position)
		{
			return 1;
		}

		protected virtual ItemEntry CacheChildLaneAndSpan(View child, Direction direction)
		{
			// Do nothing by default.
			return null;
		}

		protected virtual ItemEntry CacheChildFrame(View child, Rect childFrame)
		{
			// Do nothing by default.
			return null;
		}


		public override bool CheckLayoutParams(RecyclerView.LayoutParams lp)
		{
			if (IsVertical())
				return lp.Width == ViewGroup.LayoutParams.MatchParent;
			return lp.Height == ViewGroup.LayoutParams.MatchParent;
		}


		public override RecyclerView.LayoutParams GenerateDefaultLayoutParams()
		{
			if (IsVertical())
				return new RecyclerView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
			return new RecyclerView.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent);
		}

		public override RecyclerView.LayoutParams GenerateLayoutParams(ViewGroup.LayoutParams lp)
		{
			var lanedLp = new RecyclerView.LayoutParams((ViewGroup.MarginLayoutParams) lp);
			if (IsVertical())
			{
				lanedLp.Width = ViewGroup.LayoutParams.MatchParent;
				lanedLp.Height = lp.Height;
			}
			else
			{
				lanedLp.Width = lp.Width;
				lanedLp.Height = ViewGroup.LayoutParams.MatchParent;
			}

			return lanedLp;
		}

		public override RecyclerView.LayoutParams GenerateLayoutParams(Context c, IAttributeSet attrs)
		{
			return new RecyclerView.LayoutParams(c, attrs);
		}

		public abstract int GetLaneCount();
		public abstract void GetLaneForPosition(Lanes.LaneInfo outInfo, int position, Direction direction);

		public abstract void MoveLayoutToPosition(int position, int offset, RecyclerView.Recycler recycler,
			RecyclerView.State state);

		public class ItemEntry : Object, IParcelable
		{
			public int anchorLane;

			private int[] spanMargins;

			public int startLane;

			public ItemEntry(int startLane, int anchorLane)
			{
				this.startLane = startLane;
				this.anchorLane = anchorLane;
			}

			public ItemEntry(Parcel @in)
			{
				startLane = @in.ReadInt();
				anchorLane = @in.ReadInt();

				var marginCount = @in.ReadInt();
				if (marginCount > 0)
				{
					spanMargins = new int[marginCount];
					for (var i = 0; i < marginCount; i++)
						spanMargins[i] = @in.ReadInt();
				}
			}

			public int DescribeContents()
			{
				return 0;
			}

			public virtual void WriteToParcel(Parcel @out, ParcelableWriteFlags flags)
			{
				@out.WriteInt(startLane);
				@out.WriteInt(anchorLane);

				var marginCount = spanMargins != null ? spanMargins.Length : 0;
				@out.WriteInt(marginCount);

				for (var i = 0; i < marginCount; i++)
					@out.WriteInt(spanMargins[i]);
			}

			public void setLane(Lanes.LaneInfo laneInfo)
			{
				startLane = laneInfo.StartLane;
				anchorLane = laneInfo.AnchorLane;
			}

			public void invalidateLane()
			{
				startLane = Lanes.NO_LANE;
				anchorLane = Lanes.NO_LANE;
				spanMargins = null;
			}

			public bool hasSpanMargins()
			{
				return spanMargins != null;
			}

			public int getSpanMargin(int index)
			{
				if (spanMargins == null)
					return 0;

				return spanMargins[index];
			}

			public void setSpanMargin(int index, int margin, int span)
			{
				if (spanMargins == null)
					spanMargins = new int[span];

				spanMargins[index] = margin;
			}

/*
	public static Creator<ItemEntry> CREATOR

			= new Creator<ItemEntry>() {
			@Override

			public ItemEntry createFromParcel(Parcel @in)
	{
		return new ItemEntry(in);
	}
	
			public override ItemEntry[] newArray(int size)
			{
				return new ItemEntry[size];
			}
		}
		*/
		}

		private enum UpdateOp
		{
			ADD,
			REMOVE,
			UPDATE,
			MOVE
		}

		private class LanedSavedState : SavedState
		{
			public ItemEntries itemEntries;
			public Rect[] lanes;
			public int laneSize;

			public Orientation orientation;

			public LanedSavedState(IParcelable superState) : base(superState)
			{
				;
			}

			public LanedSavedState(Parcel @in) : base(@in)
			{
				orientation = (Orientation) @in.ReadInt();
				laneSize = @in.ReadInt();

				var laneCount = @in.ReadInt();
				if (laneCount > 0)
				{
					lanes = new Rect[laneCount];
					for (var i = 0; i < laneCount; i++)
					{
						var lane = new Rect();
						lane.ReadFromParcel(@in);
						lanes[i] = lane;
					}
				}

				var itemEntriesCount = @in.ReadInt();
				if (itemEntriesCount > 0)
				{
					itemEntries = new ItemEntries();
					for (var i = 0; i < itemEntriesCount; i++)
					{
						var entry = (ItemEntry) @in.ReadParcelable(Class.ClassLoader);
						itemEntries.RestoreItemEntry(i, entry);
					}
				}
			}

			public static IParcelableCreator CREATOR => new Creator();

			public override void WriteToParcel(Parcel @out, ParcelableWriteFlags flags)
			{
				base.WriteToParcel(@out, flags);

				@out.WriteInt((int) orientation);
				@out.WriteInt(laneSize);

				var laneCount = lanes != null ? lanes.Length : 0;
				@out.WriteInt(laneCount);

				for (var i = 0; i < laneCount; i++)
					lanes[i].WriteToParcel(@out, Rect.InterfaceConsts.ParcelableWriteReturnValue);

				var itemEntriesCount = itemEntries != null ? itemEntries.Size() : 0;
				@out.WriteInt(itemEntriesCount);

				for (var i = 0; i < itemEntriesCount; i++)
					@out.WriteParcelable(itemEntries.GetItemEntry(i), flags);
			}

			private class Creator : Object, IParcelableCreator
			{
				public Object CreateFromParcel(Parcel source)
				{
					return new LanedSavedState(source);
				}

				public Object[] NewArray(int size)
				{
					return new LanedSavedState[size];
				}
			}
		}
	}
}