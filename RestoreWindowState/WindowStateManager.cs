using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RestoreWindowState
{
	public class WindowStateManager
	{
		// �R���X�g���N�^
		public WindowStateManager()
		{
			IgnoreTitles = 
			[
				"Microsoft Text Input Application",
				"Program Manager"
			];
		}
		// ��������E�B���h�E�^�C�g���i���S��v�A�����j
		public static string[] IgnoreTitles { get; set; } = Array.Empty<string>();

		//API�Ƃ̃f�[�^�󂯓n���p�̍\���̂��`
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

		// showCmd�l
		private const int SW_SHOWNORMAL = 1;
		private const int SW_SHOWMINIMIZED = 2;
		private const int SW_SHOWMAXIMIZED = 3;

		private class WindowInfo
		{
			public string? Title { get; set; }
			// WINDOWPLACEMENT
			public WINDOWPLACEMENT WinPlacement { get; set; }
		}

		// �E�B���h�E�����擾���ĕۑ�
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
					if (length == 0) return true; // �^�C�g���Ȃ��̓X�L�b�v

					StringBuilder builder = new StringBuilder(length + 1);
					GetWindowText(hWnd, builder, builder.Capacity);

					// IgnoreTitles�z��Ɉ�v����^�C�g��������΃X�L�b�v
					if (IgnoreTitles != null && IgnoreTitles.Length > 0 &&
						IgnoreTitles.Contains(builder.ToString()))
					{
						return true;
					}

					WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
					if (!GetWindowPlacement(hWnd, ref placement)) return true; // �E�B���h�E�̔z�u�����擾�ł��Ȃ������ꍇ�̓X�L�b�v

					if (placement.showCmd == SW_SHOWMINIMIZED) return true; // �ŏ�������Ă���E�B���h�E�̓X�L�b�v

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
			{	//�t�@�C�������݂��Ȃ����͋�̃��X�g��Ԃ��ďI��
				return [];
			}
			else
			{   // �t�@�C�������݂���ꍇ�́AJSON��ǂݍ���ŃE�B���h�E���𕜌�
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
				// �ŏ�������Ă���E�B���h�E�̓X�L�b�v
				if (win.WinPlacement.showCmd == SW_SHOWMINIMIZED)
					continue;

				IntPtr hWnd = FindWindowByTitle(win.Title!);
				if (hWnd == IntPtr.Zero)
					continue;

				WINDOWPLACEMENT placement = win.WinPlacement;

				// ��Ԃ�ݒ�
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
