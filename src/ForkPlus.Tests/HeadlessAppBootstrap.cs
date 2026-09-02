// ForkPlus.Tests 全部 headless UI 测试的统一启动器（"归一"，2026-09-02）：
// MenuWindowSmokeTests / UiSmokeHeadlessTests / DetachedPopupBehaviorTests /
// ResourceCompatTests 共享同一个进程级 headless Application——继承真实 ForkPlus.App，
// 拿到全套 App.axaml 资源（FluentTheme / OxyPlot / AvaloniaEdit 主题 / 合并字典），
// 只 override 掉启动副作用（IPC 单例、主窗口、git 版本弹窗）。
//
// 为什么不再用各测试类自带的 EnsureStarted + SpinUntil(5s/30s)：
//   1) Dispatcher.UIThread 是进程级单例，首个触碰它的线程成为 owner。xUnit 默认跨
//      Collection 并行，任意并行测试（直接或经生产代码间接）先碰 Dispatcher.UIThread，
//      headless 启动线程初始化 Compositor 时即抛 "different thread owns it"，未处理
//      异常直接崩掉 test host——MIGRATION.md 记录的偶发 "Test Run Aborted" 根因。
//   2) SpinUntil 带超时：冷启 JIT 慢时"超时后继续"，worker 线程抢走 Dispatcher 归属，
//      启动线程随即崩溃；且 5s/30s 两档不一致，行为随机器负载漂移。
//   3) 4 份启动代码配了 2 种 App（真实 App / 裸 FluentTheme App），先启动者胜出——
//      后续类复用哪个 App 取决于执行顺序，需要真实资源的测试结果顺序相关。
//
// 启动时序（两层保障）：
//   [ModuleInitializer] 程序集加载期（任何测试代码执行前）spawn 启动线程——注意只
//   spawn 不等待：模块初始化器在加载器锁下运行，app 线程初始化要加载 Avalonia/
//   ForkPlus 程序集、触碰本模块类型，若在此阻塞等待会死锁（已实证：testhost 永久挂起）。
//   启动线程第一件事"抢占" Dispatcher.UIThread 归属（先于耗时 JIT/Setup，窗口微秒级，
//   任何测试线程都赶不上），随后正常 Setup——Compositor 初始化的 VerifyAccess 恒通过。
//   测试线程经 EnsureStarted 无超时等待就绪（发生在模块初始化完成之后，无锁风险）。
// 启动失败不在 ModuleInit 抛（避免连累非 headless 测试加载程序集），记入 startupError，
// 由首个 headless 测试经 EnsureStarted 显式抛出。
using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;

namespace ForkPlus.Tests
{
	internal static class HeadlessAppBootstrap
	{
		// 继承真实 App：构造时 InitializeComponent() 加载 App.axaml 全部资源/样式/DataTemplates；
		// 只 override 掉启动逻辑（IPC 单例、主窗口、git 版本弹窗等），保真且不依赖桌面。
		private sealed class HeadlessRealApp : global::ForkPlus.App
		{
			public override void OnFrameworkInitializationCompleted()
			{
			}
		}

		private static readonly ManualResetEvent Ready = new ManualResetEvent(false);
		private static int startRequested;
		private static volatile Exception startupError;

		[System.Runtime.CompilerServices.ModuleInitializer]
		internal static void ModuleInit()
		{
			StartAsync();
		}

		// 所有 headless 测试类的唯一入口：确保 App 已就绪。等待无超时上限——"超时后继续"
		// 会让 worker 抢走 Dispatcher 归属（见类注释 2），宁可挂住暴露真实启动问题。
		// 此时模块初始化早已完成，阻塞等待没有加载锁风险。
		internal static void EnsureStarted()
		{
			StartAsync();
			Ready.WaitOne();
			if (startupError != null)
			{
				throw new InvalidOperationException("headless Application 启动失败：" + startupError.Message, startupError);
			}
		}

		// 在 UI 线程执行 func 并排空 job 队列（原 4 个测试类各自的 Run<T> 收拢于此）。
		internal static T Run<T>(Func<T> func)
		{
			EnsureStarted();
			return Dispatcher.UIThread.InvokeAsync(delegate
			{
				T result = func();
				Dispatcher.UIThread.RunJobs();
				return result;
			}).GetAwaiter().GetResult();
		}

		internal static void Run(Action action)
		{
			Run<object>(delegate
			{
				action();
				return null;
			});
		}

		private static void StartAsync()
		{
			if (Interlocked.Exchange(ref startRequested, 1) == 0)
			{
				var t = new Thread(delegate()
				{
					try
					{
						// ⚠️ 第一件事抢占 Dispatcher.UIThread 归属（进程级单例、首触线程拥有）：
						// 必须发生在耗时的 JIT/App 构造之前，赶在任何并行测试线程触碰之前——
						// 否则下方 Setup 里 Compositor 初始化的 VerifyAccess 直接崩掉 test host。
						GC.KeepAlive(Dispatcher.UIThread);
						// SetupWithClassicDesktopLifetime（而非 SetupWithoutStarting）：挂上
						// ClassicDesktopStyleApplicationLifetime——WpfApp.Windows / ShowDialog
						// 兼容层从它取窗口列表；lifetime 赋值必须发生在 Setup 之前，不能用
						// SetupWithoutStarting + 后补（之后赋值 Application 会抛异常）。
						AppBuilder.Configure<HeadlessRealApp>()
							.UseHeadless(new AvaloniaHeadlessPlatformOptions())
							.SetupWithClassicDesktopLifetime(Array.Empty<string>(), delegate { });
						// 默认 ShutdownMode.OnLastWindowClose：单个测试关闭唯一窗口会把
						// Dispatcher 整个 shut down，后续测试的 InvokeAsync 全部
						// TaskCanceledException。测试由 xunit 进程托管生命周期，改显式关闭。
						if (Application.Current.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime desktopLifetime)
						{
							desktopLifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
						}
					}
					catch (Exception e)
					{
						startupError = e;
						Ready.Set();
						return;
					}
					Ready.Set();
					Dispatcher.UIThread.MainLoop(new CancellationToken());
				});
				t.IsBackground = true;
				t.Start();
			}
		}
	}
}
