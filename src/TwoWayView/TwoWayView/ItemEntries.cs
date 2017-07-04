#region

using System;
using Java.Util;

#endregion

namespace TwoWayView.Layout
{
	internal class ItemEntries
	{
		private static readonly int MIN_SIZE = 10;
		private int _adapterSize;

		private BaseLayoutManager.ItemEntry[] _itemEntries;
		private bool _restoringItem;

		private int SizeForPosition(int position)
		{
			var len = _itemEntries.Length;
			while (len <= position)
				len *= 2;

			// We don't apply any constraints while restoring
			// item entries.
			if (!_restoringItem && len > _adapterSize)
				len = _adapterSize;

			return len;
		}

		private void EnsureSize(int position)
		{
			if (_itemEntries == null)
			{
				_itemEntries = new BaseLayoutManager.ItemEntry[Math.Max(position, MIN_SIZE) + 1];
				Arrays.Fill(_itemEntries, null);
			}
			else if (position >= _itemEntries.Length)
			{
				var oldItemEntries = _itemEntries;
				_itemEntries = new BaseLayoutManager.ItemEntry[SizeForPosition(position)];
				//JavaSystem.Arraycopy(oldItemEntries, 0, mItemEntries, 0, oldItemEntries.Length);
				Array.Copy(oldItemEntries, _itemEntries, oldItemEntries.Length);
				Arrays.Fill(_itemEntries, oldItemEntries.Length, _itemEntries.Length, null);
			}
		}

		public BaseLayoutManager.ItemEntry GetItemEntry(int position)
		{
			if (_itemEntries == null || position >= _itemEntries.Length)
				return null;

			return _itemEntries[position];
		}

		public void PutItemEntry(int position, BaseLayoutManager.ItemEntry entry)
		{
			EnsureSize(position);
			_itemEntries[position] = entry;
		}

		public void RestoreItemEntry(int position, BaseLayoutManager.ItemEntry entry)
		{
			_restoringItem = true;
			PutItemEntry(position, entry);
			_restoringItem = false;
		}

		public int Size()
		{
			return _itemEntries != null ? _itemEntries.Length : 0;
		}

		public void SetAdapterSize(int adapterSize)
		{
			_adapterSize = adapterSize;
		}

		public void invalidateItemLanesAfter(int position)
		{
			if (_itemEntries == null || position >= _itemEntries.Length)
				return;

			for (var i = position; i < _itemEntries.Length; i++)
			{
				var entry = _itemEntries[i];
				if (entry != null)
					entry.invalidateLane();
			}
		}

		public void Clear()
		{
			if (_itemEntries != null)
				Arrays.Fill(_itemEntries, null);
		}

		public void OffsetForRemoval(int positionStart, int itemCount)
		{
			if (_itemEntries == null || positionStart >= _itemEntries.Length)
				return;

			EnsureSize(positionStart + itemCount);
			Array.Copy(_itemEntries, positionStart + itemCount, _itemEntries, positionStart,
				_itemEntries.Length - positionStart - itemCount);
			// TODO:Arrays.Fill
			Arrays.Fill(_itemEntries, _itemEntries.Length - itemCount, _itemEntries.Length, null);
		}

		public void OffsetForAddition(int positionStart, int itemCount)
		{
			if (_itemEntries == null || positionStart >= _itemEntries.Length)
				return;

			EnsureSize(positionStart + itemCount);

			Array.Copy(_itemEntries, positionStart, _itemEntries, positionStart + itemCount,
				_itemEntries.Length - positionStart - itemCount);
			// TODO:Arrays.Fill
			Arrays.Fill(_itemEntries, positionStart, positionStart + itemCount, null);
		}
	}
}