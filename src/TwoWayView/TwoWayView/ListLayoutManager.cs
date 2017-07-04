#region

using Android.Content;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Widget;
using TwoWayview.Layout;
using TwoWayView.Core;

#endregion

namespace TwoWayView.Layout
{
	public class ListLayoutManager : BaseLayoutManager
	{
		public ListLayoutManager(Context context, IAttributeSet attrs) : this(context, attrs, 0)
		{
			
		}

		public ListLayoutManager(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
		{
			
		}

		public ListLayoutManager(Context context, Orientation orientation) : base(orientation)
		{
			
		}

		public override int GetLaneCount()
		{
			return 1;
		}

		public override void GetLaneForPosition(Lanes.LaneInfo outInfo, int position, Direction direction)
		{
			outInfo.Set(0, 0);
		}

		public override void MoveLayoutToPosition(int position, int offset, RecyclerView.Recycler recycler,
			RecyclerView.State state)
		{
			GetLanes().Reset(offset);
		}
	}
}