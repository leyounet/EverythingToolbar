﻿using CSDeskBand;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EverythingToolbar
{
	/// <summary>
	/// Interaction logic for UserControl1.xaml
	/// </summary>
	public partial class ToolbarControl : UserControl
    {
		#region Everything
		const int EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
		const int EVERYTHING_REQUEST_HIGHLIGHTED_FILE_NAME = 0x00002000;
		const int EVERYTHING_SORT_NAME_ASCENDING = 1;
		const int EVERYTHING_SORT_NAME_DESCENDING = 2;
		const int EVERYTHING_SORT_PATH_ASCENDING = 3;
		const int EVERYTHING_SORT_PATH_DESCENDING = 4;
		const int EVERYTHING_SORT_SIZE_ASCENDING = 5;
		const int EVERYTHING_SORT_SIZE_DESCENDING = 6;
		const int EVERYTHING_SORT_DATE_CREATED_ASCENDING = 11;
		const int EVERYTHING_SORT_DATE_CREATED_DESCENDING = 12;
		const int EVERYTHING_SORT_DATE_MODIFIED_ASCENDING = 13;
		const int EVERYTHING_SORT_DATE_MODIFIED_DESCENDING = 14;

		[DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
		public static extern UInt32 Everything_SetSearchW(string lpSearchString);
		[DllImport("Everything64.dll")]
		public static extern void Everything_SetMatchPath(bool bEnable);
		[DllImport("Everything64.dll")]
		public static extern void Everything_SetMatchCase(bool bEnable);
		[DllImport("Everything64.dll")]
		public static extern void Everything_SetMatchWholeWord(bool bEnable);
		[DllImport("Everything64.dll")]
		public static extern void Everything_SetRegex(bool bEnable);
		[DllImport("Everything64.dll")]
		public static extern void Everything_SetMax(UInt32 dwMax);
		[DllImport("Everything64.dll")]
		public static extern void Everything_SetOffset(UInt32 dwOffset);
		[DllImport("Everything64.dll")]
		public static extern bool Everything_QueryW(bool bWait);
		[DllImport("Everything64.dll")]
		public static extern UInt32 Everything_GetNumResults();
		[DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
		public static extern void Everything_GetResultFullPathNameW(UInt32 nIndex, StringBuilder lpString, UInt32 nMaxCount);
		[DllImport("Everything64.dll")]
		public static extern void Everything_SetSort(UInt32 dwSortType);
		[DllImport("Everything64.dll")]
		public static extern void Everything_SetRequestFlags(UInt32 dwRequestFlags);
		[DllImport("Everything64.dll")]
		public static extern bool Everything_GetResultDateModified(UInt32 nIndex, out long lpFileTime);
		[DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr Everything_GetResultHighlightedFileName(UInt32 nIndex);
		#endregion

		#region Context Menu
		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.Save();
		}

		private void MenuItem_SortBy_Click(object sender, RoutedEventArgs e)
		{
			MenuItem itemChecked = (MenuItem)sender;
			MenuItem itemParent = (MenuItem)itemChecked.Parent;

			for (int i = 0; i < itemParent.Items.Count; i++)
			{
				if (itemParent.Items[i] == itemChecked)
				{
					Properties.Settings.Default.sortBy = i;
					continue;
				}

				(itemParent.Items[i] as MenuItem).IsChecked = false;
			}

			// Prevent the selected item from being unchecked
			(itemParent.Items[Properties.Settings.Default.sortBy] as MenuItem).IsChecked = true;

			Properties.Settings.Default.Save();
		}
		#endregion

		public ObservableCollection<SearchResult> searchResults = new ObservableCollection<SearchResult>();
		private CancellationTokenSource updateSource;
		private CancellationToken updateToken;
		private bool preventScrollChangedEvent = false;
		private string currentSearchTerm = "";
		private readonly int searchBlockSize = 100;
		private double scrollOffsetBeforeSearch = 0;
		private readonly TaskbarInfo taskbarInfo;

		public ToolbarControl(TaskbarInfo info)
		{
			InitializeComponent();
			taskbarInfo = info;
			(SortByMenu.Items[Properties.Settings.Default.sortBy] as MenuItem).IsChecked = true;
		}

		private void OpenSearchResultsWindow()
		{
			switch (taskbarInfo.Edge)
			{
				case Edge.Top:
					searchResultsPopup.Placement = PlacementMode.Bottom;
					break;
				case Edge.Left:
					searchResultsPopup.Placement = PlacementMode.Right;
					break;
				case Edge.Right:
					searchResultsPopup.Placement = PlacementMode.Left;
					break;
				case Edge.Bottom:
					searchResultsPopup.Placement = PlacementMode.Top;
					break;
			}

			AdjustStyle(taskbarInfo.Edge);
			Dispatcher.BeginInvoke((Action)(() => TabControl.SelectedIndex = 0));
			searchResultsPopup.IsOpen = true;
			searchResultsPopup.StaysOpen = true;
		}

		private void CloseSearchResultsWindow()
		{
			searchResultsPopup.IsOpen = false;
			searchResults.Clear();
		}

		public void StartSearch(string searchTerm)
		{
			scrollOffsetBeforeSearch = 0;
			searchResults.Clear();
			currentSearchTerm = searchTerm;
			RequestSearchResults(0, searchBlockSize);
		}

		private void RequestSearchResults(int offset, int count)
		{
			preventScrollChangedEvent = true;

			updateSource?.Cancel();
			updateSource = new CancellationTokenSource();
			updateToken = updateSource.Token;

			Task.Run(() =>
			{
				Everything_SetSearchW(currentSearchTerm);
				Everything_SetRequestFlags(EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME | EVERYTHING_REQUEST_HIGHLIGHTED_FILE_NAME);

				switch (Properties.Settings.Default.sortBy)
				{
					case 0:
						Everything_SetSort(EVERYTHING_SORT_NAME_ASCENDING);
						break;
					case 1:
						Everything_SetSort(EVERYTHING_SORT_NAME_DESCENDING);
						break;
					case 2:
						Everything_SetSort(EVERYTHING_SORT_PATH_ASCENDING);
						break;
					case 3:
						Everything_SetSort(EVERYTHING_SORT_PATH_DESCENDING);
						break;
					case 4:
						Everything_SetSort(EVERYTHING_SORT_SIZE_ASCENDING);
						break;
					case 5:
						Everything_SetSort(EVERYTHING_SORT_SIZE_DESCENDING);
						break;
					case 6:
						Everything_SetSort(EVERYTHING_SORT_DATE_CREATED_ASCENDING);
						break;
					case 7:
						Everything_SetSort(EVERYTHING_SORT_DATE_CREATED_DESCENDING);
						break;
					case 8:
						Everything_SetSort(EVERYTHING_SORT_DATE_MODIFIED_ASCENDING);
						break;
					case 9:
						Everything_SetSort(EVERYTHING_SORT_DATE_MODIFIED_DESCENDING);
						break;
				}

				Everything_SetMatchCase(Properties.Settings.Default.isMatchCase);
				Everything_SetMatchPath(Properties.Settings.Default.isMatchPath);
				Everything_SetMatchWholeWord(Properties.Settings.Default.isMatchWholeWord);
				Everything_SetRegex(Properties.Settings.Default.isRegExEnabled);
				Everything_SetMax((uint)count);
				Everything_SetOffset((uint)offset);
				Everything_QueryW(true);

				uint resultsCount = Everything_GetNumResults();

				for (uint i = 0; i < resultsCount; i++)
				{
					Everything_GetResultDateModified(i, out long date_modified);
					string filename = Marshal.PtrToStringUni(Everything_GetResultHighlightedFileName(i)).ToString();
					StringBuilder full_path = new StringBuilder(4096);
					Everything_GetResultFullPathNameW(i, full_path, 4096);

					Dispatcher.Invoke(() =>
					{
						searchResults.Add(new SearchResult()
						{
							FullPathAndFileName = full_path.ToString(),
							FileName = filename
						});
					});
				}

				Dispatcher.Invoke(new Action(() =>
				{
					Decorator listViewBorder = VisualTreeHelper.GetChild(SearchResultsListView, 0) as Decorator;
					ScrollViewer listViewScrollViewer = listViewBorder.Child as ScrollViewer;
					listViewScrollViewer.ScrollToVerticalOffset(scrollOffsetBeforeSearch);
					preventScrollChangedEvent = false;
				}), DispatcherPriority.ContextIdle);
			}, updateToken);
		}

		public void SelectNextSearchResult()
		{
			if (SearchResultsListView.SelectedIndex + 1 < SearchResultsListView.Items.Count)
			{
				SearchResultsListView.SelectedIndex++;
				SearchResultsListView.ScrollIntoView(SearchResultsListView.SelectedItem);
			}
		}

		public void SelectPreviousSearchResult()
		{
			if (SearchResultsListView.SelectedIndex > 0)
			{
				SearchResultsListView.SelectedIndex--;
				SearchResultsListView.ScrollIntoView(SearchResultsListView.SelectedItem);
			}
		}

		public void OpenSelectedSearchResult()
		{
			if (SearchResultsListView.SelectedIndex != -1)
			{
				try
				{
					Process.Start((SearchResultsListView.SelectedItem as SearchResult).FullPathAndFileName);
				}
				catch (Win32Exception)
				{
					MessageBox.Show("Failed to open this file/folder.");
				}
			}
		}

		public void AdjustStyle(Edge edge)
		{
			switch (edge)
			{
				case Edge.Top:
					searchResultsPopupBorder.BorderThickness = new Thickness(1, 0, 1, 1);
					break;
				case Edge.Left:
					searchResultsPopupBorder.BorderThickness = new Thickness(0, 1, 1, 1);
					break;
				case Edge.Right:
					searchResultsPopupBorder.BorderThickness = new Thickness(1, 1, 0, 1);
					break;
				case Edge.Bottom:
					searchResultsPopupBorder.BorderThickness = new Thickness(1, 1, 1, 0);
					break;
			}

			SearchResultsListView.Height = searchResultsPopup.Height - TabControl.Height;
		}

		private void SearchResultsPopup_Closed(object sender, EventArgs e)
		{
			searchBox.Clear();
		}

		private void SearchResultsListViewItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			SearchResultsListView.SelectedItems.Clear();

			ListViewItem item = sender as ListViewItem;
			if (item != null)
			{
				item.IsSelected = true;
				SearchResultsListView.SelectedItem = item;
			}
		}

		private void SearchResultsListViewItem_PreviewMouseUp(object sender, MouseButtonEventArgs e)
		{
			ListViewItem item = sender as ListViewItem;
			if (item != null && item.IsSelected)
			{
				OpenSelectedSearchResult();
			}
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!searchResultsPopup.IsMouseOver)
			{
				return;
			}

			if (currentSearchTerm.StartsWith("folder:") || currentSearchTerm.StartsWith("folders:") || currentSearchTerm.StartsWith("file:") || currentSearchTerm.StartsWith("files:"))
			{
				currentSearchTerm = currentSearchTerm.Split(new[] { ':' }, 2)[1];
			}

			if (FilesTab.IsSelected)
			{
				currentSearchTerm = "file:" + currentSearchTerm;
			}
			else if (FoldersTab.IsSelected)
			{
				currentSearchTerm = "folder:" + currentSearchTerm;
			}

			StartSearch(currentSearchTerm);
		}

		private void ListView_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (preventScrollChangedEvent)
			{
				e.Handled = true;
				return;
			}

			if (e.VerticalChange > 0)
			{
				if (e.VerticalOffset > e.ExtentHeight - 2 * e.ViewportHeight)
				{
					scrollOffsetBeforeSearch = e.VerticalOffset;
					RequestSearchResults(searchResults.Count, searchBlockSize);
				}
			}
		}

		private void SearchBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
		{
			searchResultsPopup.StaysOpen = false;
		}

		private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
		{
			Keyboard.ClearFocus();
		}

		private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (searchBox.Text.Length == 0)
			{
				CloseSearchResultsWindow();
				return;
			}

			if (SearchResultsListView.ItemsSource == null)
				SearchResultsListView.ItemsSource = searchResults;

			OpenSearchResultsWindow();
			StartSearch(searchBox.Text);
		}

		private void CSDeskBandWpf_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (!searchResultsPopup.IsOpen)
				return;

			if (e.Key == Key.Up)
			{
				SelectPreviousSearchResult();
			}
			else if (e.Key == Key.Down)
			{
				SelectNextSearchResult();
			}
			else if (e.Key == Key.Enter)
			{
				OpenSelectedSearchResult();
			}
			else if (e.Key == Key.Escape)
			{
				CloseSearchResultsWindow();
				Keyboard.ClearFocus();
			}
		}
	}
}