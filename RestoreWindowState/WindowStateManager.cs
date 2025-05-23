using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
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
			public string? ClassName { get; set; }
			public uint ProcessId { get; set; }
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
					StringBuilder className = new StringBuilder(256);
					NativeMethods.GetClassName(hWnd, className, className.Capacity);
					NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);

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
						ClassName = className.ToString(),
						ProcessId = processId,
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
				IncludeFields = true,
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
			};
			string json = JsonSerializer.Serialize(winlist, options);
			File.WriteAllText(filePath, json, Encoding.UTF8);
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
				string json = File.ReadAllText(filePath, Encoding.UTF8);
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

				IntPtr hWnd = FindWindowByInfo(win.Title!, win.ClassName!, win.ProcessId);
				if (hWnd == IntPtr.Zero)
					continue;

				NativeMethods.WINDOWPLACEMENT placement = win.WinPlacement;

				if (win.WinPlacement.showCmd == NativeMethods.SW_SHOWMAXIMIZED)
				{
					// 1. 通常状態で配置
					placement.showCmd = NativeMethods.SW_SHOWNORMAL;
					NativeMethods.SetWindowPlacement(hWnd, ref placement);

					// 2. 最大化状態で再配置
					placement.showCmd = NativeMethods.SW_SHOWMAXIMIZED;
					NativeMethods.SetWindowPlacement(hWnd, ref placement);
				}
				else
				{
					// 通常状態で配置
					placement.showCmd = NativeMethods.SW_SHOWNORMAL;
					NativeMethods.SetWindowPlacement(hWnd, ref placement);
				}
			}
		}

		private static IntPtr FindWindowByInfo(string title, string className, uint processId)
		{
			IntPtr found = IntPtr.Zero;
			NativeMethods.EnumWindows((hWnd, lParam) =>
			{
				int length = NativeMethods.GetWindowTextLength(hWnd);
				if (length == 0) return true;
				StringBuilder builder = new StringBuilder(length + 1);
				NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);

				StringBuilder classBuilder = new StringBuilder(256);
				NativeMethods.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

				NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);

				if (builder.ToString() == title &&
					classBuilder.ToString() == className &&
					pid == processId)
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
