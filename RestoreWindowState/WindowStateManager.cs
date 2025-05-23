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

		//APIとのデータ受け渡し用の構造体を定義
		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int X;
			public int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct WINDOWPLACEMENT
		{
			public int length;
			public int flags;
			public int showCmd;
			public POINT ptMinPosition;
			public POINT ptMaxPosition;
			public RECT rcNormalPosition;
		}

		// showCmd値
		private const int SW_SHOWNORMAL = 1;
		private const int SW_SHOWMINIMIZED = 2;
		private const int SW_SHOWMAXIMIZED = 3;

		private class WindowInfo
		{
			public string? Title { get; set; }
			// WINDOWPLACEMENT
			public WINDOWPLACEMENT WinPlacement { get; set; }
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
			_ = EnumWindows((hWnd, lParam) =>
			{
				if (IsWindowVisible(hWnd))
				{
					int length = GetWindowTextLength(hWnd);
					if (length == 0) return true; // タイトルなしはスキップ

					StringBuilder builder = new StringBuilder(length + 1);
					GetWindowText(hWnd, builder, builder.Capacity);

					// IgnoreTitles配列に一致するタイトルがあればスキップ
					if (IgnoreTitles != null && IgnoreTitles.Length > 0 &&
						IgnoreTitles.Contains(builder.ToString()))
					{
						return true;
					}

					WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
					if (!GetWindowPlacement(hWnd, ref placement)) return true; // ウィンドウの配置情報を取得できなかった場合はスキップ

					if (placement.showCmd == SW_SHOWMINIMIZED) return true; // 最小化されているウィンドウはスキップ

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
				if (win.WinPlacement.showCmd == SW_SHOWMINIMIZED)
					continue;

				IntPtr hWnd = FindWindowByTitle(win.Title!);
				if (hWnd == IntPtr.Zero)
					continue;

				WINDOWPLACEMENT placement = win.WinPlacement;

				// 状態を設定
				if (win.WinPlacement.showCmd == SW_SHOWMAXIMIZED)
					placement.showCmd = SW_SHOWMAXIMIZED;
				else
					placement.showCmd = SW_SHOWNORMAL;

				SetWindowPlacement(hWnd, ref placement);
			}
		}

		private static IntPtr FindWindowByTitle(string title)
		{
			IntPtr found = IntPtr.Zero;
			EnumWindows((hWnd, lParam) =>
			{
				int length = GetWindowTextLength(hWnd);
				if (length == 0) return true;
				StringBuilder builder = new StringBuilder(length + 1);
				GetWindowText(hWnd, builder, builder.Capacity);
				if (builder.ToString() == title)
				{
					found = hWnd;
					return false;
				}
				return true;
			}, IntPtr.Zero);
			return found;
		}

		private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);
	}
}
