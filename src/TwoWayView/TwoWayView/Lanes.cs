#region

using System;
using Android.Graphics;
using Android.Widget;
using TwoWayView.Core;
using TwoWayView.Layout;

#endregion

namespace TwoWayview.Layout
{
	public class Lanes
	{
		public static int NO_LANE = -1;
		private int? _innerEnd;

		private int? _innerStart;
		private readonly bool _isVertical;
		private readonly Rect[] _lanes;
		private readonly int _laneSize;

		private BaseLayoutManager _layout;
		private readonly Rect[] _savedLanes;
		private readonly LaneInfo _tempLaneInfo = new LaneInfo();

		private readonly Rect _tempRect = new Rect();

		public Lanes(BaseLayoutManager layout, Orientation orientation, Rect[] lanes, int laneSize)
		{
			_layout = layout;
			_isVertical = orientation == Orientation.Vertical;
			_lanes = lanes;
			_laneSize = laneSize;

			_savedLanes = new Rect[_lanes.Length];
			for (var i = 0; i < _lanes.Length; i++)
				_savedLanes[i] = new Rect();
		}

		public Lanes(BaseLayoutManager layout, int laneCount)
		{
			_layout = layout;
			_isVertical = layout.IsVertical();

			_lanes = new Rect[laneCount];
			_savedLanes = new Rect[laneCount];
			for (var i = 0; i < laneCount; i++)
			{
				_lanes[i] = new Rect();
				_savedLanes[i] = new Rect();
			}

			_laneSize = CalculateLaneSize(layout, laneCount);

			var paddingLeft = layout.PaddingLeft;
			var paddingTop = layout.PaddingTop;

			for (var i = 0; i < laneCount; i++)
			{
				var laneStart = i * _laneSize;

				var l = paddingLeft + (_isVertical ? laneStart : 0);
				var t = paddingTop + (_isVertical ? 0 : laneStart);
				var r = _isVertical ? l + _laneSize : l;
				var b = _isVertical ? t : t + _laneSize;

				_lanes[i].Set(l, t, r, b);
			}
		}

		public int Count => _lanes.Length;

		public static int CalculateLaneSize(BaseLayoutManager layout, int laneCount)
		{
			if (layout.IsVertical())
			{
				var paddingLeft = layout.PaddingLeft;
				var paddingRight = layout.PaddingRight;
				var width = layout.Width - paddingLeft - paddingRight;
				return width / laneCount;
			}
			var paddingTop = layout.PaddingTop;
			var paddingBottom = layout.PaddingBottom;
			var height = layout.Height - paddingTop - paddingBottom;
			return height / laneCount;
		}

		private void InvalidateEdges()
		{
			_innerStart = null;
			_innerEnd = null;
		}

		public Orientation GetOrientation()
		{
			return _isVertical ? Orientation.Vertical : Orientation.Horizontal;
		}

		public void Save()
		{
			for (var i = 0; i < _lanes.Length; i++)
				_savedLanes[i].Set(_lanes[i]);
		}

		public void Restore()
		{
			for (var i = 0; i < _lanes.Length; i++)
				_lanes[i].Set(_savedLanes[i]);
		}

		public int GetLaneSize()
		{
			return _laneSize;
		}

		public int GetCount()
		{
			return _lanes.Length;
		}

		private void OffsetLane(int lane, int offset)
		{
			_lanes[lane]
				.Offset(_isVertical ? 0 : offset,
					_isVertical ? offset : 0);
		}

		public void Offset(int offset)
		{
			for (var i = 0; i < _lanes.Length; i++)
				this.Offset(i, offset);

			InvalidateEdges();
		}

		public void Offset(int lane, int offset)
		{
			OffsetLane(lane, offset);
			InvalidateEdges();
		}

		public void GetLane(int lane, Rect laneRect)
		{
			try
			{
				laneRect.Set(_lanes[lane]);
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.TraceWarning(e.ToString());
			}
			
		}

		public int PushChildFrame(Rect outRect, int lane, int margin, Direction direction)
		{
			int delta;

			var laneRect = _lanes[lane];
			if (_isVertical)
			{
				if (direction == Direction.End)
				{
					delta = outRect.Top - laneRect.Bottom;
					laneRect.Bottom = outRect.Bottom + margin;
				}
				else
				{
					delta = outRect.Bottom - laneRect.Top;
					laneRect.Top = outRect.Top - margin;
				}
			}
			else
			{
				if (direction == Direction.End)
				{
					delta = outRect.Left - laneRect.Right;
					laneRect.Right = outRect.Right + margin;
				}
				else
				{
					delta = outRect.Right - laneRect.Left;
					laneRect.Left = outRect.Left - margin;
				}
			}

			InvalidateEdges();

			return delta;
		}

		public void PopChildFrame(Rect outRect, int lane, int margin, Direction direction)
		{
			var laneRect = _lanes[lane];
			try
			{
				if (_isVertical)
				{
					if (direction == Direction.End)
						laneRect.Top = outRect.Bottom - margin;
					else
						laneRect.Bottom = outRect.Top + margin;
				}
				else
				{
					if (direction == Direction.End)
						laneRect.Left = outRect.Right - margin;
					else
						laneRect.Right = outRect.Left + margin;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}			

			InvalidateEdges();
		}

		public void GetChildFrame(Rect outRect, int childWidth, int childHeight, LaneInfo laneInfo,
			Direction direction)
		{
			var startRect = _lanes[laneInfo.StartLane];

			// The anchor lane only applies when we're get child frame in the direction
			// of the forward scroll. We'll need to rethink this once we start working on
			// RTL support.
			var anchorLane =
				direction == Direction.End ? laneInfo.AnchorLane : laneInfo.StartLane;
			var anchorRect = _lanes[anchorLane];

			if (_isVertical)
			{
				outRect.Left = startRect.Left;
				outRect.Top =
					direction == Direction.End ? anchorRect.Bottom : anchorRect.Top - childHeight;
			}
			else
			{
				outRect.Top = startRect.Top;
				outRect.Left =
					direction == Direction.End ? anchorRect.Right : anchorRect.Left - childWidth;
			}

			outRect.Right = outRect.Left + childWidth;
			outRect.Bottom = outRect.Top + childHeight;
		}

		private bool Intersects(int start, int count, Rect r)
		{
			for (var l = start; l < start + count; l++)
				if (Rect.Intersects(_lanes[l], r))
					return true;

			return false;
		}

		private int FindLaneThatFitsSpan(int anchorLane, int laneSpan, Direction direction)
		{
			var findStart = Math.Max(0, anchorLane - laneSpan + 1);
			var findEnd = Math.Min(findStart + laneSpan, _lanes.Length - laneSpan + 1);
			for (var l = findStart; l < findEnd; l++)
			{
				_tempLaneInfo.Set(l, anchorLane);

				GetChildFrame(_tempRect, _isVertical ? laneSpan * _laneSize : 1,
					_isVertical ? 1 : laneSpan * _laneSize, _tempLaneInfo, direction);

				if (!Intersects(l, laneSpan, _tempRect))
					return l;
			}

			return NO_LANE;
		}

		public void FindLane(LaneInfo outInfo, int laneSpan, Direction direction)
		{
			outInfo.SetUndefined();

			var targetEdge = direction == Direction.End ? int.MaxValue : int.MinValue;
			for (var l = 0; l < _lanes.Length; l++)
			{
				int laneEdge;
				if (_isVertical)
					laneEdge = direction == Direction.End ? _lanes[l].Bottom : _lanes[l].Top;
				else
					laneEdge = direction == Direction.End ? _lanes[l].Right : _lanes[l].Left;

				if (direction == Direction.End && laneEdge < targetEdge ||
				    direction == Direction.Start && laneEdge > targetEdge)
				{
					var targetLane = FindLaneThatFitsSpan(l, laneSpan, direction);
					if (targetLane != NO_LANE)
					{
						targetEdge = laneEdge;
						outInfo.Set(targetLane, l);
					}
				}
			}
		}

		public void Reset(Direction direction)
		{
			for (var i = 0; i < _lanes.Length; i++)
			{
				var laneRect = _lanes[i];
				if (_isVertical)
				{
					if (direction == Direction.Start)
						laneRect.Bottom = laneRect.Top;
					else
						laneRect.Top = laneRect.Bottom;
				}
				else
				{
					if (direction == Direction.Start)
						laneRect.Right = laneRect.Left;
					else
						laneRect.Left = laneRect.Right;
				}
			}

			InvalidateEdges();
		}

		public void Reset(int offset)
		{
			for (var i = 0; i < _lanes.Length; i++)
			{
				var laneRect = _lanes[i];

				laneRect.OffsetTo(_isVertical ? laneRect.Left : offset,
					_isVertical ? offset : laneRect.Top);

				if (_isVertical)
					laneRect.Bottom = laneRect.Top;
				else
					laneRect.Right = laneRect.Left;
			}

			InvalidateEdges();
		}

		public int GetInnerStart()
		{
			if (_innerStart != null)
				return _innerStart.Value;

			_innerStart = int.MinValue;
			for (var i = 0; i < _lanes.Length; i++)
			{
				var laneRect = _lanes[i];
				_innerStart = Math.Max(_innerStart.Value, _isVertical ? laneRect.Top : laneRect.Left);
			}

			return _innerStart.Value;
		}

		public int GetInnerEnd()
		{
			if (_innerEnd != null)
				return _innerEnd.Value;

			_innerEnd = int.MaxValue;
			for (var i = 0; i < _lanes.Length; i++)
			{
				var laneRect = _lanes[i];
				_innerEnd = Math.Min(_innerEnd.Value, _isVertical ? laneRect.Bottom : laneRect.Right);
			}

			return _innerEnd.Value;
		}

		public class LaneInfo
		{
			public int AnchorLane;
			public int StartLane;

			public bool IsUndefined()
			{
				return StartLane == NO_LANE || AnchorLane == NO_LANE;
			}

			public void Set(int startLane, int anchorLane)
			{
				this.StartLane = startLane;
				this.AnchorLane = anchorLane;
			}

			public void SetUndefined()
			{
				StartLane = NO_LANE;
				AnchorLane = NO_LANE;
			}
		}
	}
}