#region

using System;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using TwoWayview.Layout;
using TwoWayView.Core;
using Exception = Java.Lang.Exception;
using Math = System.Math;

#endregion

namespace TwoWayView.Layout
{
	public class SpannableGridLayoutManager : GridLayoutManager
	{
		private static readonly int DEFAULT_NUM_COLS = 3;
		private static readonly int DEFAULT_NUM_ROWS = 3;
		private bool _measuring;

		public SpannableGridLayoutManager(Context context) : this(context, null)
		{
			
		}

		public SpannableGridLayoutManager(Context context, IAttributeSet attrs) : this(context, attrs, 0)
		{
			
		}

		public SpannableGridLayoutManager(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle,
			DEFAULT_NUM_COLS, DEFAULT_NUM_ROWS)
		{
			
		}

		public SpannableGridLayoutManager(Orientation orientation, int numColumns, int numRows) : base(orientation,
			numColumns, numRows)
		{
			
		}

		private int GetChildWidth(int colSpan)
		{
			return GetLanes().GetLaneSize() * colSpan;
		}

		private int GetChildHeight(int rowSpan)
		{
			return GetLanes().GetLaneSize() * rowSpan;
		}

		private static int GetLaneSpan(LayoutParams lp, bool isVertical)
		{
			return isVertical ? lp.ColSpan : lp.RowSpan;
		}

		private static int GetLaneSpan(SpannableItemEntry entry, bool isVertical)
		{
			return isVertical ? entry.ColSpan : entry.RowSpan;
		}


		public override bool CanScrollHorizontally()
		{
			return base.CanScrollHorizontally() && !_measuring;
		}

		public override bool CanScrollVertically()
		{
			return base.CanScrollVertically() && !_measuring;
		}

		public override int GetLaneSpanForChild(View child)
		{
			return GetLaneSpan((LayoutParams) child.LayoutParameters, IsVertical());
		}

		public override int GetLaneSpanForPosition(int position)
		{
			var entry = (SpannableItemEntry) GetItemEntryForPosition(position);
			if (entry == null)
				throw new IllegalStateException("Could not find span for position " + position);

			return GetLaneSpan(entry, IsVertical());
		}


		public override void GetLaneForPosition(Lanes.LaneInfo outInfo, int position, Direction direction)
		{
			var entry = (SpannableItemEntry) GetItemEntryForPosition(position);
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

		private int GetWidthUsed(View child)
		{
			var lp = (LayoutParams) child.LayoutParameters;
			return Width - PaddingLeft - PaddingRight - GetChildWidth(lp.ColSpan);
		}

		private int GetHeightUsed(View child)
		{
			var lp = (LayoutParams) child.LayoutParameters;
			return Height - PaddingTop - PaddingBottom - GetChildHeight(lp.RowSpan);
		}

		protected override void MeasureChildWithMargins(View child)
		{
// XXX: This will disable scrolling while measuring this child to ensure that
// both width and height can use MATCH_PARENT properly.
			_measuring = true;
			MeasureChildWithMargins(child, GetWidthUsed(child), GetHeightUsed(child));
			_measuring = false;
		}


		public override void MoveLayoutToPosition(int position, int offset, RecyclerView.Recycler recycler,
			RecyclerView.State state)
		{
			var isVertical = IsVertical();
			var lanes = GetLanes();

			lanes.Reset(0);

			for (var i = 0; i <= position; i++)
			{
				var entry = (SpannableItemEntry) GetItemEntryForPosition(i);
				if (entry == null)
				{
					var child = recycler.GetViewForPosition(i);
					entry = (SpannableItemEntry) CacheChildLaneAndSpan(child, Direction.End);
				}

				TempLaneInfo.Set(entry.startLane, entry.anchorLane);

// The lanes might have been invalidated because an added or
// removed item. See BaseLayoutManager.invalidateItemLanes().
				if (TempLaneInfo.IsUndefined())
				{
					lanes.FindLane(TempLaneInfo, GetLaneSpanForPosition(i), Direction.End);
					entry.setLane(TempLaneInfo);
				}

				lanes.GetChildFrame(TempRect, GetChildWidth(entry.ColSpan),
					GetChildHeight(entry.RowSpan), TempLaneInfo, Direction.End);

				if (i != position)
					PushChildFrame(entry, TempRect, entry.startLane, GetLaneSpan(entry, isVertical),
						Direction.End);
			}
			lanes.GetLane(TempLaneInfo.StartLane, TempRect);
			lanes.Reset(Direction.End);
			lanes.Offset(offset - (isVertical ? TempRect.Bottom : TempRect.Right));	
		}

		protected override ItemEntry CacheChildLaneAndSpan(View child, Direction direction)
		{
			var position = GetPosition(child);

			TempLaneInfo.SetUndefined();

			var entry = (SpannableItemEntry) GetItemEntryForPosition(position);
			if (entry != null)
				TempLaneInfo.Set(entry.startLane, entry.anchorLane);

			if (TempLaneInfo.IsUndefined())
				GetLaneForChild(TempLaneInfo, child, direction);

			if (entry == null)
			{
				var lp = (LayoutParams) child.LayoutParameters;
				entry = new SpannableItemEntry(TempLaneInfo.StartLane, TempLaneInfo.AnchorLane,
					lp.ColSpan, lp.RowSpan);
				SetItemEntryForPosition(position, entry);
			}
			else
			{
				entry.setLane(TempLaneInfo);
			}

			return entry;
		}

		public override bool CheckLayoutParams(RecyclerView.LayoutParams lp)
		{
			if (lp.Width != ViewGroup.LayoutParams.MatchParent ||
			    lp.Height != ViewGroup.LayoutParams.MatchParent)
				return false;

			if (lp is LayoutParams)
			{
				var spannableLp = (LayoutParams) lp;

				if (IsVertical())
					return spannableLp.RowSpan >= 1 && spannableLp.ColSpan >= 1 &&
					       spannableLp.ColSpan <= GetLaneCount();
				return spannableLp.ColSpan >= 1 && spannableLp.RowSpan >= 1 &&
				       spannableLp.RowSpan <= GetLaneCount();
			}

			return false;
		}

		public override RecyclerView.LayoutParams GenerateDefaultLayoutParams()
		{
			return new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
		}

		public override RecyclerView.LayoutParams GenerateLayoutParams(ViewGroup.LayoutParams lp)
		{
			var spannableLp = new LayoutParams((ViewGroup.MarginLayoutParams) lp);
			spannableLp.Width = ViewGroup.LayoutParams.MatchParent;
			spannableLp.Height = ViewGroup.LayoutParams.MatchParent;

			if (lp is LayoutParams)
			{
				var other = (LayoutParams) lp;
				if (IsVertical())
				{
					spannableLp.ColSpan = Math.Max(1, Math.Min(other.ColSpan, GetLaneCount()));
					spannableLp.RowSpan = Math.Max(1, other.RowSpan);
				}
				else
				{
					spannableLp.ColSpan = Math.Max(1, other.ColSpan);
					spannableLp.RowSpan = Math.Max(1, Math.Min(other.RowSpan, GetLaneCount()));
				}
			}

			return spannableLp;
		}


		public override RecyclerView.LayoutParams GenerateLayoutParams(Context c, IAttributeSet attrs)
		{
			return new LayoutParams(c, attrs);
		}

		protected class SpannableItemEntry : ItemEntry
		{
			public static AnonymousIParcelableCreator<SpannableItemEntry> Creator
				= new AnonymousIParcelableCreator<SpannableItemEntry>(s => new SpannableItemEntry(s));

			public int ColSpan;
			public int RowSpan;

			public SpannableItemEntry(int startLane, int anchorLane, int colSpan, int rowSpan) : base(startLane, anchorLane)
			{
				this.ColSpan = colSpan;
				this.RowSpan = rowSpan;
			}

			public SpannableItemEntry(Parcel @in) : base(@in)
			{
				ColSpan = @in.ReadInt();
				RowSpan = @in.ReadInt();
			}

			public override void WriteToParcel(Parcel @out, ParcelableWriteFlags flags)
			{
				base.WriteToParcel(@out, flags);
				@out.WriteInt(ColSpan);
				@out.WriteInt(RowSpan);
			}
		}

		public class LayoutParams : RecyclerView.LayoutParams
		{
			private static readonly int DEFAULT_SPAN = 1;

			public LayoutParams(int width, int height) : this(width, height, DEFAULT_SPAN, DEFAULT_SPAN)
			{
			}

			public LayoutParams(int width, int height, int rowSpan, int colSpan) : base(width, height)
			{
				RowSpan = rowSpan;
				ColSpan = colSpan;
			}

			public LayoutParams(Context c, IAttributeSet attrs) : base(c, attrs)
			{
				var a = c.ObtainStyledAttributes(attrs, Resource.Styleable.twowayview_SpannableGridViewChild);
				ColSpan = Math.Max(
					DEFAULT_SPAN, a.GetInt(Resource.Styleable.twowayview_SpannableGridViewChild_twowayview_colSpan, -1));
				RowSpan = Math.Max(
					DEFAULT_SPAN, a.GetInt(Resource.Styleable.twowayview_SpannableGridViewChild_twowayview_rowSpan, -1));

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

			public int ColSpan { get; set; }
			public int RowSpan { get; set; }

			private void Init(ViewGroup.LayoutParams other)
			{
				if (other is LayoutParams)
				{
					var lp = (LayoutParams) other;
					RowSpan = lp.RowSpan;
					ColSpan = lp.ColSpan;
				}
				else
				{
					RowSpan = DEFAULT_SPAN;
					ColSpan = DEFAULT_SPAN;
				}
			}
		}
	}
}