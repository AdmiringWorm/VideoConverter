namespace VideoConverter.Commands
{
    using System;
    using System.Threading;
    using System.Runtime.InteropServices;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Humanizer;
    using ShellProgressBar;
    using Spectre.Cli;
    using VideoConverter.Options;
    using VideoConverter.Storage.Models;
    using VideoConverter.Storage.Repositories;
    using Xabe.FFmpeg;
    using System.Security.Cryptography;
    using System.Text;
    using Spectre.Console;

    public class EncodeCommand : AsyncCommand<EncodeOption>
    {
        private readonly QueueRepository queueRepo;
        private readonly Configuration config;
        private readonly CancellationToken cancellationToken;
        private readonly IAnsiConsole console;

        public EncodeCommand(QueueRepository queueRepo, Configuration config, IAnsiConsole console)
        {
            this.queueRepo = queueRepo;
            this.config = config;
            var tokenSource = new CancellationTokenSource();
            this.cancellationToken = tokenSource.Token;

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Cancelling!!!");
                if (e.SpecialKey == ConsoleSpecialKey.ControlC)
                {
                    tokenSource.Cancel();
                    e.Cancel = true;
                }
            };
            this.console = console;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, EncodeOption settings)
        {
            Console.Clear();
            var failed = false;
            if (settings.IncludeFailing)
            {
                this.queueRepo.ResetFailedQueue();
                this.queueRepo.SaveChanges();
            }

            var count = GetPendingCount(0, settings.Indexes);

            if (count == 0)
            {
                return 1;
            }

            var progressOptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                EnableTaskBarProgress = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                ForegroundColor = ConsoleColor.Cyan,
                ForegroundColorDone = ConsoleColor.DarkCyan,
            };

            var childOptions = new ProgressBarOptions
            {
                ProgressCharacter = progressOptions.ProgressCharacter,
                ForegroundColor = ConsoleColor.Magenta,
                ProgressBarOnBottom = progressOptions.ProgressBarOnBottom
            };
            var encodingOptions = new ProgressBarOptions
            {
                ProgressCharacter = childOptions.ProgressCharacter,
                ForegroundColor = ConsoleColor.DarkYellow,
                ProgressBarOnBottom = childOptions.ProgressBarOnBottom,
                CollapseWhenFinished = true,
            };

            using var pbMain = new ProgressBar(count, string.Empty, progressOptions);

            int indexCount = 0;
            FileQueue? queue = GetNextQueueItem(settings.Indexes, ref indexCount);

            while (queue != null)
            {
                try
                {
                    this.queueRepo.SaveChanges();
                }
                catch (Exception ex)
                {
                    this.console.WriteException(ex);
                    return 1;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return 1;
                }

                pbMain.Message = $"Processing file {pbMain.CurrentTick + 1} / {pbMain.MaxTicks}";

                var newFileName = Path.GetFileNameWithoutExtension(queue.OutputPath);

                int maxSteps = 5;
                if (settings.RemoveOldFiles)
                    maxSteps++;

                using var stepChild = pbMain.Spawn(maxSteps, "Collecting information", childOptions);


                try
                {

                    var mediaInfo = await FFmpeg.GetMediaInfo(queue.Path);

                    stepChild.Tick("Setting encoding options");

                    var directory = Path.GetDirectoryName(queue.OutputPath) ?? Environment.CurrentDirectory;
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var streams = mediaInfo.Streams.Where(s => queue.Streams.Contains(s.Index));

                    var conversion = FFmpeg.Conversions.New();

                    foreach (var stream in streams)
                    {
                        if (stream is IVideoStream videoStream)
                            videoStream.SetCodec(this.config.VideoCodec);
                        else if (stream is IAudioStream audioStream)
                            audioStream.SetCodec(this.config.AudioCodec);
                        else if (stream is ISubtitleStream subtitleStream)
                            subtitleStream.SetCodec(this.config.SubtitleCodec);
                        conversion.AddStream(stream);
                    }

                    var tempWorkPath = Path.Combine(this.config.WorkDirectory, Guid.NewGuid() + Path.GetExtension(queue.OutputPath));

                    conversion.AddParameter("-movflags +faststart")
                        .SetOverwriteOutput(true)
                        .SetOutput(tempWorkPath);

                    stepChild.Tick($"Encoding '{newFileName}'...");
                    this.queueRepo.UpdateQueueStatus(queue.Id, QueueStatus.Encoding);
                    this.queueRepo.SaveChanges();

                    using (var encodingPb = stepChild.Spawn(100, $"Initialized", encodingOptions))
                    {
                        conversion.OnProgress += (sender, args) =>
                        {
                            if (encodingPb.MaxTicks == 100)
                                encodingPb.MaxTicks = (int)args.TotalLength.TotalSeconds;
                            encodingPb.Tick((int)args.Duration.TotalSeconds, $"Completed: {args.Duration} / Total: {args.TotalLength}");
                        };

                        try
                        {
                            var initialSize = new FileInfo(queue.Path).Length;
                            await conversion.Start(cancellationToken);
                            if (cancellationToken.IsCancellationRequested)
                            {
                                failed = true;
                                this.queueRepo.UpdateQueueStatus(queue.Id, QueueStatus.Pending, "Progress was cancelled by user");
                                stepChild.ForegroundColor = ConsoleColor.DarkGray;
                                stepChild.Tick(stepChild.CurrentTick, $"Encoding Cancelled for '{newFileName}'");
                                if (File.Exists(tempWorkPath))
                                    File.Delete(tempWorkPath);
                            }
                            else
                            {
                                stepChild.Tick($"Calculating new hash for '{newFileName}'");
                                var newHash = await GetSHA1Async(tempWorkPath, CancellationToken.None);
                                queue.NewHash = newHash;

                                var isDuplicate = this.queueRepo.FileExists(queue.Path, newHash);
                                queue.Status = QueueStatus.Completed;

                                if (isDuplicate)
                                {
                                    queue.StatusMessage = "Duplicate file...";
                                    if (settings.IgnoreDuplicates)
                                    {
                                        stepChild.Tick($"Removing duplicate file...");
                                        File.Delete(tempWorkPath);
                                        stepChild.ForegroundColor = ConsoleColor.DarkGray;
                                    }
                                }

                                if (!isDuplicate || !settings.IgnoreDuplicates)
                                {
                                    var newSize = new FileInfo(tempWorkPath).Length;
                                    if (newSize > initialSize)
                                        queue.StatusMessage = $"Lost {(newSize - initialSize).Bytes().Humanize("#.##")}";
                                    else if (newSize < initialSize)
                                        queue.StatusMessage = $"Saved {(initialSize - newSize).Bytes().Humanize("#.##")}";
                                    else
                                        queue.StatusMessage = $"No loss or gain in size";
                                }

                                this.queueRepo.UpdateQueue(queue);

                                //this.queueRepo.UpdateQueueStatus(queue.Id, QueueStatus.Completed, statusMessage);
                                encodingPb.Tick(encodingPb.MaxTicks, "Completed");
                                if (!isDuplicate || !settings.IgnoreDuplicates)
                                {
                                    stepChild.Tick($"Moving encoded file to new location '{newFileName}'");
                                    if (File.Exists(queue.OutputPath))
                                        File.Delete(queue.OutputPath);
                                    File.Move(tempWorkPath, queue.OutputPath);
                                    stepChild.ForegroundColor = ConsoleColor.DarkGreen;
                                }

                                if (settings.RemoveOldFiles)
                                {
                                    stepChild.Tick($"Removing old encoded file '{Path.GetFileNameWithoutExtension(queue.Path)}");
                                    if (queue.Path != queue.OutputPath && File.Exists(queue.Path))
                                        File.Delete(queue.Path);
                                }


                                stepChild.Tick($"Encoding completed for '{newFileName}'");
                            }
                        }
                        catch (Exception ex)
                        {
                            failed = true;
                            if (cancellationToken.IsCancellationRequested)
                            {
                                this.queueRepo.UpdateQueueStatus(queue.Id, QueueStatus.Pending, "Progress was cancelled by user");
                                stepChild.ForegroundColor = ConsoleColor.DarkGray;
                                stepChild.Tick(stepChild.CurrentTick, $"Encoding Cancelled for '{newFileName}'");
                            }
                            else
                            {
                                stepChild.ForegroundColor = ConsoleColor.DarkRed;
                                this.queueRepo.UpdateQueueStatus(queue.Id, QueueStatus.Failed, ex);
                                stepChild.Tick(stepChild.CurrentTick, $"Encoding failed for '{newFileName}'");
                            }

                            if (File.Exists(tempWorkPath))

                                File.Delete(tempWorkPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    stepChild.ForegroundColor = ConsoleColor.DarkRed;
                    this.queueRepo.UpdateQueueStatus(queue.Id, QueueStatus.Failed, ex);
                    stepChild.Tick(stepChild.CurrentTick, $"Encoding failed for '{newFileName}'");
                    failed = true;
                }

                try
                {
                    this.queueRepo.SaveChanges();

                    pbMain.Tick($"Completed file {pbMain.CurrentTick + 1} / {pbMain.MaxTicks}");

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var newCount = GetPendingCount(pbMain.CurrentTick, settings.Indexes);

                        if (newCount != count)
                        {
                            count = newCount;
                            pbMain.MaxTicks = count;
                        }


                        queue = GetNextQueueItem(settings.Indexes, ref indexCount);
                    }
                }
                catch (Exception ex)
                {
                    this.console.WriteException(ex);
                    return 1;
                }
            }

            return failed ? 1 : 0;
        }

        private int GetPendingCount(int currentTick, int[] indexes)
        {
            if (indexes is not null && indexes.Length > 0)
                return indexes.Length;

            return this.queueRepo.GetPendingQueueCount() + currentTick;
        }

        private FileQueue? GetNextQueueItem(int[] indexes, ref int indexCount)
        {
            FileQueue? queue = null;
            if (indexes is not null && indexes.Length > 0)
            {
                if (indexCount < indexes.Length)
                {
                    queue = this.queueRepo.GetQueueItem(indexes[indexCount]);
                    indexCount++;
                }
            }
            else
            {
                queue = this.queueRepo.GetNextQueueItem();
            }

            return queue;
        }

        private async Task<string> GetSHA1Async(string file, CancellationToken cancellationToken)
        {
            using var algo = SHA1.Create();
            using var stream = File.OpenRead(file);
            var sb = new StringBuilder();

            var hashBytes = await algo.ComputeHashAsync(stream, cancellationToken);

            foreach (var b in hashBytes)
            {
                sb.AppendFormat("{0:x2}", b);
            }

            return sb.ToString();
        }
    }
}
