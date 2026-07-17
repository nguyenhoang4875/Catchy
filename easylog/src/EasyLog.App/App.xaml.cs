using System.IO;
using System.Windows;
using System.Windows.Threading;
using EasyLog.App.Infrastructure;

namespace EasyLog.App;

public partial class App : Application
{
	private static readonly string DiagnosticLogPath = Path.Combine(Path.GetTempPath(), AppMetadata.ProductFolderName, "logs", "app-startup.log");

	public App()
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		try
		{
			base.OnStartup(e);

			var window = new MainWindow();
			MainWindow = window;
			window.Show();
		}
		catch (Exception ex)
		{
			ReportFatalError("앱 시작 중 예외가 발생했습니다.", ex);
			Shutdown(-1);
		}
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		ReportFatalError("UI 스레드 예외가 발생했습니다.", e.Exception);
		e.Handled = true;
		Shutdown(-1);
	}

	private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			ReportFatalError("처리되지 않은 예외가 발생했습니다.", ex);
		}
		else
		{
			ReportFatalMessage("처리되지 않은 알 수 없는 예외가 발생했습니다.");
		}
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		ReportFatalError("백그라운드 작업 예외가 발생했습니다.", e.Exception);
		e.SetObserved();
	}

	private static void ReportFatalError(string message, Exception ex)
	{
		try
		{
			var logMessage = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
			Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticLogPath)!);
			File.AppendAllText(DiagnosticLogPath, logMessage);
		}
		catch
		{
			// 로그 기록 실패는 무시
		}

		MessageBox.Show(
			$"{message}{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}로그: {DiagnosticLogPath}",
			$"{AppMetadata.ProductName} 오류",
			MessageBoxButton.OK,
			MessageBoxImage.Error);
	}

	private static void ReportFatalMessage(string message)
	{
		MessageBox.Show(
			$"{message}{Environment.NewLine}{Environment.NewLine}로그: {DiagnosticLogPath}",
			$"{AppMetadata.ProductName} 오류",
			MessageBoxButton.OK,
			MessageBoxImage.Error);
	}
}


