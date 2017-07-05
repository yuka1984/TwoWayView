#region

using System;
using Android.Content;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Widget;
using Java.Lang;
using TwoWayview.Layout;
using TwoWayView.Core;
using Math = System.Math;

#endregion

namespace TwoWayView.Layout
{
	public class GridLayoutManager : BaseLayoutManager
	{
		private static readonly int DEFAULT_NUM_COLS = 2;
		private static readonly int DEFAULT_NUM_ROWS = 2;

		private int _numColumns;
		private int _numRows;
		public GridLayoutManager(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{

		}

		public GridLayoutManager(Context context, IAttributeSet attrs) : this(context, attrs, 0)
		{
			;
		}

		public GridLayoutManager(Context context, IAttributeSet attrs, int defStyle) : this(context, attrs, defStyle,
			DEFAULT_NUM_COLS, DEFAULT_NUM_ROWS)
		{
			
		}

		protected GridLayoutManager(Context context, IAttributeSet attrs, int defStyle,
			int defaultNumColumns, int defaultNumRows) : base(context, attrs, defStyle)
		{			
			var a =
				context.ObtainStyledAttributes(attrs, Resource.Styleable.twowayview_GridLayoutManager, defStyle, 0);

			_numColumns =
				Math.Max(1, a.GetInt(Resource.Styleable.twowayview_GridLayoutManager_twowayview_numColumns, defaultNumColumns));
			_numRows =
				Math.Max(1, a.GetInt(Resource.Styleable.twowayview_GridLayoutManager_twowayview_numRows, defaultNumRows));

			a.Recycle();
		}

		public GridLayoutManager(Orientation orientation, int numColumns, int numRows) : base(orientation)
		{
			_numColumns = numColumns;
			_numRows = numRows;

			if (_numColumns < 1)
				throw new IllegalArgumentException("GridLayoutManager must have at least 1 column");

			if (_numRows < 1)
				throw new IllegalArgumentException("GridLayoutManager must have at least 1 row");
		}

		public override int GetLaneCount()
		{
			return IsVertical() ? _numColumns : _numRows;
		}

		public override void GetLaneForPosition(Lanes.LaneInfo outInfo, int position, Direction direction)
		{
			var lane = position % GetLaneCount();
			outInfo.Set(lane, lane);
		}

		public override void MoveLayoutToPosition(int position, int offset, RecyclerView.Recycler recycler,
			RecyclerView.State state)
		{
			var lanes = GetLanes();
			lanes.Reset(offset);

			GetLaneForPosition(TempLaneInfo, position, Direction.End);
			var lane = TempLaneInfo.StartLane;
			if (lane == 0)
				return;

			var child = recycler.GetViewForPosition(position);
			MeasureChild(child, Direction.End);

			var dimension =
				IsVertical() ? GetDecoratedMeasuredHeight(child) : GetDecoratedMeasuredWidth(child);

			for (var i = lane - 1; i >= 0; i--)
				lanes.Offset(i, dimension);
		}

		public int GetNumColumns()
		{
			return _numColumns;
		}

		public void SetNumColumns(int numColumns)
		{
			if (_numColumns == numColumns)
				return;

			_numColumns = numColumns;
			if (IsVertical())
				RequestLayout();
		}

		public int GetNumRows()
		{
			return _numRows;
		}

		public void SetNumRows(int numRows)
		{
			if (_numRows == numRows)
				return;

			_numRows = numRows;
			if (!IsVertical())
				RequestLayout();
		}
	}
}