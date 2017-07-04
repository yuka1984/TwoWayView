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
using Math = System.Math;

#endregion

namespace TwoWayView.Layout
{
	public class StaggeredGridLayoutManager : GridLayoutManager
	{
		private static readonly int DEFAULT_NUM_COLS = 2;
		private static readonly int DEFAULT_NUM_ROWS = 2;


		public StaggeredGridLayoutManager(Context context) : this(context, null)
		{
			;
		}

		public StaggeredGridLayoutManager(Context context, IAttributeSet attrs) : this(context, attrs, 0)
		{
			;
		}

		public StaggeredGridLayoutManager(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle,
			DEFAULT_NUM_COLS, DEFAULT_NUM_ROWS)
		{
			;
		}

		public StaggeredGridLayoutManager(Orientation orientation, int numColumns, int numRows) : base(orientation,
			numColumns, numRows)
		{
			;
		}

		public override int GetLaneSpanForChild(View child)
		{
			var lp = (LayoutParams) child.LayoutParameters;
			return lp.Span;
		}


		public override int GetLaneSpanForPosition(int position)
		{
			var entry = (StaggeredItemEntry) GetItemEntryForPosition(position);
			if (entry == null)
				throw new IllegalStateException("Could not find span for position " + position);

			return entry.Span;
		}

		public override void GetLaneForPosition(Lanes.LaneInfo outInfo, int position, Direction direction)
		{
			var entry = (StaggeredItemEntry) GetItemEntryForPosition(position);
			if (entry != null)
			{
				outInfo.Set(entry.startLane, entry.anchorLane);
				return;
			}

			outInfo.SetUndefined();
		}


		protected override void GetLaneForChild(Lanes.LaneInfo outInfo, View child, Direction direction)
		{
			base.GetLaneForChild(outInfo, child, direction);
			if (outInfo.IsUndefined())
				GetLanes().FindLane(outInfo, GetLaneSpanForChild(child), direction);
		}

		public override void MoveLayoutToPosition(int position, int offset, RecyclerView.Recycler recycler,
			RecyclerView.State state)
		{
			var isVertical = IsVertical();
			var lanes = GetLanes();

			lanes.Reset(0);

			for (var i = 0; i <= position; i++)
			{
				var entry = (StaggeredItemEntry) GetItemEntryForPosition(i);

				if (entry != null)
				{
					TempLaneInfo.Set(entry.startLane, entry.anchorLane);

// The lanes might have been invalidated because an added or
// removed item. See BaseLayoutManager.invalidateItemLanes().
					if (TempLaneInfo.IsUndefined())
					{
						lanes.FindLane(TempLaneInfo, GetLaneSpanForPosition(i), Direction.End);
						entry.setLane(TempLaneInfo);
					}

					lanes.GetChildFrame(TempRect, entry.Width, entry.height, TempLaneInfo,
						Direction.End);
				}
				else
				{
					var child = recycler.GetViewForPosition(i);

// XXX: This might potentially cause stalls in the main
// thread if the layout ends up having to measure tons of
// child views. We might need to add different policies based
// on known assumptions regarding certain layouts e.g. child
// views have stable aspect ratio, lane size is fixed, etc.
					MeasureChild(child, Direction.End);

// The measureChild() call ensures an entry is created for
// this position.
					entry = (StaggeredItemEntry) GetItemEntryForPosition(i);

					TempLaneInfo.Set(entry.startLane, entry.anchorLane);
					lanes.GetChildFrame(TempRect, GetDecoratedMeasuredWidth(child),
						GetDecoratedMeasuredHeight(child), TempLaneInfo, Direction.End);

					cacheItemFrame(entry, TempRect);
				}

				if (i != position)
					PushChildFrame(entry, TempRect, entry.startLane, entry.Span, Direction.End);
			}

			lanes.GetLane(TempLaneInfo.StartLane, TempRect);
			lanes.Reset(Direction.End);
			lanes.Offset(offset - (isVertical ? TempRect.Bottom : TempRect.Right));
		}

		protected override ItemEntry CacheChildLaneAndSpan(View child, Direction direction)
		{
			var position = GetPosition(child);

			TempLaneInfo.SetUndefined();

			var entry = (StaggeredItemEntry) GetItemEntryForPosition(position);
			if (entry != null)
				TempLaneInfo.Set(entry.startLane, entry.anchorLane);

			if (TempLaneInfo.IsUndefined())
				GetLaneForChild(TempLaneInfo, child, direction);

			if (entry == null)
			{
				entry = new StaggeredItemEntry(TempLaneInfo.StartLane, TempLaneInfo.AnchorLane,
					GetLaneSpanForChild(child));
				SetItemEntryForPosition(position, entry);
			}
			else
			{
				entry.setLane(TempLaneInfo);
			}

			return entry;
		}

		private void cacheItemFrame(StaggeredItemEntry entry, Rect childFrame)
		{
			entry.Width = childFrame.Right - childFrame.Left;
			entry.height = childFrame.Bottom - childFrame.Top;
		}

		protected override ItemEntry CacheChildFrame(View child, Rect childFrame)
		{
			var entry = (StaggeredItemEntry) GetItemEntryForPosition(GetPosition(child));
			if (entry == null)
				throw new IllegalStateException("Tried to cache frame on undefined item");

			cacheItemFrame(entry, childFrame);
			return entry;
		}

		public override bool CheckLayoutParams(RecyclerView.LayoutParams lp)
		{
			var result = base.CheckLayoutParams(lp);
			if (lp is LayoutParams)
			{
				var staggeredLp = (LayoutParams) lp;
				result &= staggeredLp.Span >= 1 && staggeredLp.Span <= GetLaneCount();
			}

			return result;
		}

		public override RecyclerView.LayoutParams GenerateDefaultLayoutParams()
		{
			if (IsVertical())
				return new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
			return new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent);
		}


		public override RecyclerView.LayoutParams GenerateLayoutParams(ViewGroup.LayoutParams lp)
		{
			var staggeredLp = new LayoutParams((ViewGroup.MarginLayoutParams) lp);
			if (IsVertical())
			{
				staggeredLp.Width = ViewGroup.LayoutParams.MatchParent;
				staggeredLp.Height = lp.Height;
			}
			else
			{
				staggeredLp.Width = lp.Width;
				staggeredLp.Height = ViewGroup.LayoutParams.MatchParent;
			}

			if (lp is LayoutParams)
			{
				var other = (LayoutParams) lp;
				staggeredLp.Span = Math.Max(1, Math.Min(other.Span, GetLaneCount()));
			}

			return staggeredLp;
		}


		public override RecyclerView.LayoutParams GenerateLayoutParams(Context c, IAttributeSet attrs)
		{
			return new LayoutParams(c, attrs);
		}

		protected class StaggeredItemEntry : ItemEntry
		{
			public int height;

			internal int Span;
			internal int Width;

			public StaggeredItemEntry(int startLane, int anchorLane, int span) : base(startLane, anchorLane)
			{
				this.Span = span;
			}

			public StaggeredItemEntry(Parcel @in) : base(@in)
			{
				Span = @in.ReadInt();
				Width = @in.ReadInt();
				height = @in.ReadInt();
			}

			public override void WriteToParcel(Parcel @out, ParcelableWriteFlags flags)
			{
				base.WriteToParcel(@out, flags);
				@out.WriteInt(Span);
				@out.WriteInt(Width);
				@out.WriteInt(height);
			}

			//	TODO: Creator
			/*
				public static Parcelable.Creator<StaggeredItemEntry> CREATOR

				= new Parcelable.Creator<StaggeredItemEntry>() {
					@Override

					public StaggeredItemEntry createFromParcel(Parcel @in)
					{
						return new StaggeredItemEntry(in);
					}

					@Override
					public StaggeredItemEntry[] newArray(int size)
					{
						return new StaggeredItemEntry[size];
					}
				};
				*/
		}

		public class LayoutParams : RecyclerView.LayoutParams
		{
			private static readonly int DEFAULT_SPAN = 1;

			public int Span;

			public LayoutParams(int width, int height) : base(width, height)
			{
				Span = DEFAULT_SPAN;
			}

			public LayoutParams(Context c, IAttributeSet attrs) : base(c, attrs)
			{
				var a = c.ObtainStyledAttributes(attrs, Resource.Styleable.twowayview_StaggeredGridViewChild);
				Span = Math.Max(DEFAULT_SPAN, a.GetInt(Resource.Styleable.twowayview_StaggeredGridViewChild_twowayview_span, -1));
				a.Recycle();
			}

			public LayoutParams(ViewGroup.LayoutParams other) : base(other)
			{
				Init(other);
			}

			public LayoutParams(ViewGroup.MarginLayoutParams other) : base(other)
			{
				Init(other);
			}

			private void Init(ViewGroup.LayoutParams other)
			{
				if (other is LayoutParams)
				{
					var lp = (LayoutParams) other;
					Span = lp.Span;
				}
				else
				{
					Span = DEFAULT_SPAN;
				}
			}
		}
	}
}