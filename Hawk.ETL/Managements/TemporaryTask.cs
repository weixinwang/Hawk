﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Hawk.Core.Utils;
using Hawk.Core.Utils.Logs;

namespace Hawk.ETL.Managements
{
    public class TemporaryTask : TaskBase
    {
        private Task CurrentTask;

        public TemporaryTask()
        {
            AutoDelete = true;
            WasCanceled = false;
            WasAborted = false;
        }

        private bool WasAborted { get;  set; }
        private bool WasCanceled { get;  set; }
        public Action TaskAction { get; set; }

      

        public static TemporaryTask AddTempTask<T>(string taskName, IEnumerable<T> enumable, Action<T> action,
            Action<int> continueAction = null, int count = -1, bool autoStart = true, int notifyInterval = 1,
            Func<int> delayFunc = null)
        {
            var tempTask = new TemporaryTask {Name = taskName};
            // start a task with a means to do a hard abort (unsafe!)
            tempTask.ContinueAction = continueAction;
            var index = 0;
            if (notifyInterval <= 0)
                notifyInterval = 1;
            tempTask.TaskAction = () =>
            {
                if (enumable is ICollection<T>)
                {
                    count = ((ICollection<T>) enumable).Count;
                }

                foreach (var r in enumable)
                {
                    action?.Invoke(r);

                    if (r is int)
                    {
                        index = Convert.ToInt32(r);
                    }
                    else
                    {
                        index++;
                    }

                    if (index%notifyInterval != 0) continue;
                    if (tempTask.CheckCancel())
                    {
                        tempTask.WasCanceled = true;
                        break;
                    }
                    if (delayFunc != null)
                        Thread.Sleep(delayFunc());
                    if (count > 0)
                    {
                        tempTask.Percent = tempTask.CurrentIndex*100/count;
                    }
                    tempTask.CurrentIndex = index;
                    tempTask.CheckWait();
                }
                if (!tempTask.WasCanceled)
                {
                    XLogSys.Print.Debug($"任务【{tempTask.Name}】已经成功完成");
                    tempTask.Percent = 100;
                }
                if (continueAction != null)
                {
                    ControlExtended.UIInvoke(() => continueAction(tempTask.CurrentIndex));
                }
            };

            if (autoStart)
            {
                tempTask.Start();
            }
            return tempTask;
        }


        public static TemporaryTask AddTempTask<T>(string taskName, IEnumerable<T> source, Func<IEnumerable<T>,IEnumerable<T>> action,
          Action<int> continueAction = null, int count = -1, bool autoStart = true, int notifyInterval = 1,
          Func<int> delayFunc = null)
        {
            var tempTask = new TemporaryTask { Name = taskName };
            tempTask.ContinueAction = continueAction;
            // start a task with a means to do a hard abort (unsafe!)

            var index = 0;
            if (notifyInterval <= 0)
                notifyInterval = 1;
            tempTask.TaskAction = () =>
            {
                if (source is ICollection<T>)
                {
                    count = ((ICollection<T>)source).Count;
                }

                foreach (var r in action!=null?action(source):source)
                {
                 

                    if (r is int)
                    {
                        index = Convert.ToInt32(r);
                    }
                    else
                    {
                        index++;
                    }

                    if (index % notifyInterval != 0) continue;
                    if (tempTask.CheckCancel())
                    {
                        tempTask.WasCanceled = true;
                        break;
                    }
                    if (delayFunc != null)
                        Thread.Sleep(delayFunc());
                    if (count > 0)
                    {
                        tempTask.Percent = tempTask.CurrentIndex * 100 / count;
                    }
                    tempTask.CurrentIndex = index;
                    tempTask.CheckWait();
                }
                if (!tempTask.WasCanceled)
                {
                    XLogSys.Print.Debug($"任务【{tempTask.Name}】已经成功完成");
                    tempTask.Percent = 100;
                }
                if (continueAction != null)
                {
                    ControlExtended.UIInvoke(() => continueAction(tempTask.CurrentIndex));
                }
            };

            if (autoStart)
            {
                tempTask.Start();
            }
            return tempTask;
        }

        [Browsable(false)]
        public Action<int> ContinueAction { get; set; }


        public override void Start()
        {
            base.Start();
            if (TaskAction != null)
            {
                IsStart = true;


                CurrentTask = new Task(() =>
                {
                    try
                    {  //Capture the thread
                       var  thread = Thread.CurrentThread;
                        using (CancellationToken.Token.Register(() =>
                        {
                            Task.Factory.StartNew(() =>
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    Thread.Sleep(100);
                                    if (WasCanceled == true)
                                        break;
                                }
                                if (WasCanceled == false)
                                    thread.Abort();
                            });
                           
                        }))
                        {
                            TaskAction();
                        }
                    }
                    catch (ThreadAbortException)
                    {
                        XLogSys.Print.Warn("任务已经强行被终止");
                        IsStart = false;
                        ControlExtended.UIInvoke(()=>this.ContinueAction?.Invoke(this.CurrentIndex));
                        WasAborted = true;
                    
                    }
                    catch (Exception ex)
                    {

                        XLogSys.Print.Error("任务已经出错：" + ex);
                        IsStart = false;
                    }

                    IsStart = false;
                    if (AutoDelete)
                        Remove();
                }, CancellationToken.Token);
                CurrentTask.Start();
            }
        }

        public void Wait()
        {
            CurrentTask.Wait();
        }
    }
}