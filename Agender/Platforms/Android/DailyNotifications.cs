using System.Text;
using Agender.Shared.Abstractions;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Android.App;
using Android.App.Job;
using Android.Content;
using AndroidX.Core.App;
using Microsoft.Extensions.Logging;
using AndroidApplication = Android.App.Application;

namespace Agender;

public sealed class AndroidDailyNotifications(IServiceProvider services) : IDeviceNotifications
{
    public bool IsEnabled => DailyDigestNotifier.IsEnabled;

    public TimeOnly Time => DailyDigestNotifier.Time;

    public async Task<bool> EnableAsync(TimeOnly time, CancellationToken cancellationToken = default)
    {
        if (!await DailyDigestNotifier.EnsurePermissionAsync())
        {
            return false;
        }

        DailyDigestNotifier.Save(true, time);
        DailyDigestNotifier.Schedule(AndroidApplication.Context);
        return true;
    }

    public void Disable()
    {
        DailyDigestNotifier.Save(false, Time);
        DailyDigestNotifier.Cancel(AndroidApplication.Context);
    }

    public async Task<bool> ShowNowAsync(CancellationToken cancellationToken = default) =>
        await DailyDigestNotifier.EnsurePermissionAsync()
        && await DailyDigestNotifier.ShowAsync(services, AndroidApplication.Context, cancellationToken);
}

internal static class DailyDigestNotifier
{
    private const string EnabledKey = "agender.daily.enabled";
    private const string MinutesKey = "agender.daily.minutes";
    private const string ChannelId = "agender_daily";
    private const int NotificationId = 4101;
    private const int JobId = 4102;
    private const int AlarmRequestCode = 4103;

    public static bool IsEnabled => Preferences.Default.Get(EnabledKey, false);

    public static TimeOnly Time => TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Preferences.Default.Get(MinutesKey, (int)DailyCatalog.DefaultTime.ToTimeSpan().TotalMinutes)));

    public static void Save(bool enabled, TimeOnly time)
    {
        Preferences.Default.Set(EnabledKey, enabled);
        Preferences.Default.Set(MinutesKey, time.Hour * 60 + time.Minute);
    }

    public static async Task<bool> EnsurePermissionAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return true;
        }

        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            }

            return status == PermissionStatus.Granted;
        });
    }

    public static void Schedule(Context context)
    {
        if (!IsEnabled || context.GetSystemService(Context.AlarmService) is not AlarmManager alarms)
        {
            return;
        }

        DateTime now = DateTime.Now;
        DateTime next = DateOnly.FromDateTime(now).ToDateTime(Time);

        if (next <= now)
        {
            next = next.AddDays(1);
        }

        long trigger = new DateTimeOffset(next).ToUnixTimeMilliseconds();
        PendingIntent alarm = AlarmIntent(context);

        if (!OperatingSystem.IsAndroidVersionAtLeast(31) || alarms.CanScheduleExactAlarms())
        {
            alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, trigger, alarm);
        }
        else
        {
            alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, trigger, alarm);
        }
    }

    public static void Cancel(Context context)
    {
        if (context.GetSystemService(Context.AlarmService) is AlarmManager alarms)
        {
            alarms.Cancel(AlarmIntent(context));
        }
    }

    public static void StartJob(Context context)
    {
        if (context.GetSystemService(Context.JobSchedulerService) is not JobScheduler scheduler)
        {
            return;
        }

        JobInfo.Builder builder = new(JobId, new ComponentName(context, Java.Lang.Class.FromType(typeof(DailyDigestJob))));
        builder.SetRequiredNetworkType(NetworkType.Any);
        builder.SetOverrideDeadline((long)TimeSpan.FromMinutes(30).TotalMilliseconds);

        if (builder.Build() is JobInfo job)
        {
            scheduler.Schedule(job);
        }
    }

    public static async Task<bool> ShowAsync(IServiceProvider services, Context context, CancellationToken cancellationToken)
    {
        using IServiceScope scope = services.CreateScope();
        DailyDigestDto digest = await scope.ServiceProvider.GetRequiredService<IDailyReminderService>().GetDigestAsync(cancellationToken);

        NotificationManagerCompat notifications = NotificationManagerCompat.From(context)!;

        if (digest.Sections.Count == 0 || !notifications.AreNotificationsEnabled())
        {
            return false;
        }

        EnsureChannel(context);

        StringBuilder body = new();

        foreach (DailyDigestSection section in digest.Sections)
        {
            body.Append(DailyCatalog.Emoji(section.Section)).Append(' ').AppendLine(section.Title);

            foreach (string line in section.Lines)
            {
                body.Append("• ").AppendLine(line);
            }

            body.AppendLine();
        }

        string summary = string.Join(" · ", digest.Sections.Select(s => $"{DailyCatalog.Emoji(s.Section)} {DailyCatalog.Label(s.Section)} ({s.Lines.Count})"));
        Intent? launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        PendingIntent? open = launch is null ? null : PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        NotificationCompat.Builder builder = new(context, ChannelId);
        builder.SetSmallIcon(Resource.Drawable.ic_stat_agender);
        builder.SetContentTitle("Hoje na casa");
        builder.SetContentText(summary);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(body.ToString().TrimEnd()));
        builder.SetContentIntent(open);
        builder.SetAutoCancel(true);

        if (builder.Build() is not Notification notification)
        {
            return false;
        }

        notifications.Notify(NotificationId, notification);
        return true;
    }

    private static void EnsureChannel(Context context)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && context.GetSystemService(Context.NotificationService) is NotificationManager manager)
        {
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Resumo diário", NotificationImportance.Default)
            {
                Description = "O resumo do dia: limpezas, refeições, agenda da casa e inventário."
            });
        }
    }

    private static PendingIntent AlarmIntent(Context context) =>
        PendingIntent.GetBroadcast(context, AlarmRequestCode, new Intent(context, typeof(DailyAlarmReceiver)), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class DailyAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || !DailyDigestNotifier.IsEnabled)
        {
            return;
        }

        DailyDigestNotifier.StartJob(context);
        DailyDigestNotifier.Schedule(context);
    }
}

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced])]
public sealed class DailyBootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null && intent?.Action is Intent.ActionBootCompleted or Intent.ActionMyPackageReplaced)
        {
            DailyDigestNotifier.Schedule(context);
        }
    }
}

[Service(Permission = "android.permission.BIND_JOB_SERVICE", Exported = true)]
public sealed class DailyDigestJob : JobService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public override bool OnStartJob(JobParameters? parameters)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using CancellationTokenSource cancellation = new(Timeout);
                IServiceProvider? services = IPlatformApplication.Current?.Services;

                if (services is not null)
                {
                    await DailyDigestNotifier.ShowAsync(services, ApplicationContext!, cancellation.Token);
                }
            }
            catch (Exception ex)
            {
                IPlatformApplication.Current?.Services.GetService<ILogger<DailyDigestJob>>()?.LogWarning(ex, "Daily digest notification failed.");
            }
            finally
            {
                JobFinished(parameters, false);
            }
        });

        return true;
    }

    public override bool OnStopJob(JobParameters? parameters) => false;
}
