using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RestoreWindowState
{
	public class WindowStateManager
	{
		// コンストラクタ
		public WindowStateManager()
		{
			IgnoreTitles = 
			[
				"Microsoft Text Input Application",
				"Program Manager"
			];
		}
		// 無視するウィンドウタイトル（完全一致、複数可）
		public static string[] IgnoreTitles { get; set; } = Array.Empty<string>();

		// ウィンドウ情報を保存するためのクラス
		private class WindowInfo
		{
			public string? Title { get; set; }
			public NativeMethods.WINDOWPLACEMENT WinPlacement { get; set; }
		}
		// ウィンドウ情報を取得して保存
		public void CaptureAndSave(string filePath)
		{
			List<WindowInfo> winlist = GetAllWindows();
			SaveWindowsToJson(filePath, winlist);
		}
		public void LoadAndRestore(string filePath)
		{
			List<WindowInfo> winlist = LoadWindowsFromJson(filePath);
			RestoreAllWindows(winlist);
		}

		private static List<WindowInfo> GetAllWindows()
		{
			var windows = new List<WindowInfo>();
			_ = NativeMethods.EnumWindows((hWnd, lParam) =>
			{
				if (NativeMethods.IsWindowVisible(hWnd))
				{
					int length = NativeMethods.GetWindowTextLength(hWnd);
					if (length == 0) return true;

					StringBuilder builder = new StringBuilder(length + 1);
					NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);

					if (IgnoreTitles != null && IgnoreTitles.Length > 0 &&
						IgnoreTitles.Contains(builder.ToString()))
					{
						return true;
					}

					var placement = new NativeMethods.WINDOWPLACEMENT();
					if (!NativeMethods.GetWindowPlacement(hWnd, ref placement)) return true;

					if (placement.showCmd == NativeMethods.SW_SHOWMINIMIZED) return true;

					windows.Add(new WindowInfo
					{
						Title = builder.ToString(),
						WinPlacement = placement
					});
				}
				return true;
			}, IntPtr.Zero);
			return windows;
		}
		private static void SaveWindowsToJson(string filePath,List<WindowInfo> winlist)
		{
			var options = new JsonSerializerOptions
			{
				WriteIndented = true,
				IncludeFields = true
			};
			string json = JsonSerializer.Serialize(winlist, options);
			File.WriteAllText(filePath, json);
		}

		private static List<WindowInfo> LoadWindowsFromJson(string filePath)
		{


			if (!File.Exists(filePath))
			{	//ファイルが存在しない時は空のリストを返して終了
				return [];
			}
			else
			{   // ファイルが存在する場合は、JSONを読み込んでウィンドウ情報を復元
				List<WindowInfo> winlist;
				string json = File.ReadAllText(filePath);
				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					IncludeFields = true
				};
				winlist = JsonSerializer.Deserialize<List<WindowInfo>>(json, options) ?? new List<WindowInfo>();
				return winlist;
			}
		}

		private static void RestoreAllWindows(List<WindowInfo> winlist)
		{
			foreach (var win in winlist)
			{
				// 最小化されているウィンドウはスキップ
				if (win.WinPlacement.showCmd == NativeMethods.SW_SHOWMINIMIZED)
					continue;

				IntPtr hWnd = FindWindowByTitle(win.Title!);
				if (hWnd == IntPtr.Zero)
					continue;

				NativeMethods.WINDOWPLACEMENT placement = win.WinPlacement;

				// 状態を設定
				if (win.WinPlacement.showCmd == NativeMethods.SW_SHOWMAXIMIZED)
					placement.showCmd = NativeMethods.SW_SHOWMAXIMIZED;
				else
					placement.showCmd = NativeMethods.SW_SHOWNORMAL;

				NativeMethods.SetWindowPlacement(hWnd, ref placement);
			}
		}

		private static IntPtr FindWindowByTitle(string title)
		{
			IntPtr found = IntPtr.Zero;
			NativeMethods.EnumWindows((hWnd, lParam) =>
			{
				int length = NativeMethods.GetWindowTextLength(hWnd);
				if (length == 0) return true;
				StringBuilder builder = new StringBuilder(length + 1);
				NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
				if (builder.ToString() == title)
				{
					found = hWnd;
					return false;
				}
				return true;
			}, IntPtr.Zero);
			return found;
		}
	}
}
