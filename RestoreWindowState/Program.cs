namespace RestoreWindowState
{
	internal class Program
	{
		static void Main(string[] args)
		{
			var manager = new WindowStateManager();
			string filePath = @".\InfoWindows.json";

			// コマンドライン引数による分岐
			if (args.Length > 0)
			{
				switch (args[0].ToLower())
				{
					case "-save":
					case "-s":
						manager.CaptureAndSave(filePath);
						return;
					case "-restore":
					case "-r":
						manager.LoadAndRestore(filePath);

						return;
					default:
						return;
				}
			}
		}
	}
}