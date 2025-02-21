﻿using IBM.WMQ;
using System.Collections;
using IbmMQTestApp.Settings;
using IbmMQTestApp.Entities;


namespace IbmMQTestApp.Services
{
    public class QueueTransientService
    {
        public QueueTransientService(QueueSettings settings)
        {
            Settings = settings;
        }
        public QueueSettings Settings { get; set; }
        private MQQueueManager mqQMgr;
        private MQQueue mqQueue;

        public void InitQueue(string queue)
        {
            if (mqQMgr != null)
            {
                if (mqQueue == null)
                {
                    mqQueue = mqQMgr.AccessQueue(Settings.Queues[queue], MQC.MQOO_INPUT_SHARED | MQC.MQOO_FAIL_IF_QUIESCING | MQC.MQOO_BROWSE);
                }
                return;
            }
            Hashtable props = new()
                {
                    { MQC.HOST_NAME_PROPERTY, Settings.Host },
                    { MQC.CHANNEL_PROPERTY, Settings.Channel },
                    { MQC.PORT_PROPERTY, Settings.Port },
                    { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
                    { MQC.USER_ID_PROPERTY, Settings.Username },
                { MQC.PASSWORD_PROPERTY, Settings.Password }
                };
            try
            {
                mqQMgr = new MQQueueManager(Settings.QueueManagerName, props);
                mqQueue = mqQMgr.AccessQueue(Settings.Queues[queue], MQC.MQOO_INPUT_SHARED | MQC.MQOO_FAIL_IF_QUIESCING | MQC.MQOO_BROWSE | MQC.MQOO_INQUIRE);
            }
            catch (Exception e)
            {

                throw;
            }
        }

        public async Task StartQueueProcessor(Func<string, string, CancellationToken, Task> callBack, string queue, CancellationToken stoppingToken)
        {
            InitQueue(queue);
            bool _continue = true;
            while (_continue && !stoppingToken.IsCancellationRequested)
            {
                try
                {

                    if (IsQueueEmpty())
                    {
                        await Task.Delay(10000, stoppingToken); // Wait for some time before checking again
                        continue;
                    }

                    MQMessage mqMsg = new();
                    MQGetMessageOptions mqGetMsgOpts = new()
                    {
                        Options = MQC.MQGMO_LOCK + MQC.MQGMO_FAIL_IF_QUIESCING + MQC.MQGMO_WAIT + MQC.MQGMO_BROWSE_FIRST,
                        WaitInterval = 10000
                    };


                    mqQueue.Get(mqMsg, mqGetMsgOpts);
                    string stringMessage = mqMsg.ReadString(mqMsg.MessageLength);

                    var message = new MessageEntity(stringMessage);

                    if (!message.IsAllowedToRetry())
                    {
                        continue;
                    }

                    await callBack(queue, stringMessage, stoppingToken);

                    mqGetMsgOpts.Options = MQC.MQGMO_MSG_UNDER_CURSOR;
                    mqQueue.Get(mqMsg, mqGetMsgOpts);
                }
                catch (MQException mqe)
                {
                    if (mqe?.Reason != MQC.MQRC_NO_MSG_AVAILABLE)
                    {
                        _continue = false;
                    }
                }
                catch (Exception ex)
                {

                    _continue = false;

                }
            }
        }

        public async Task SendMessageToQueue(string queue, string message)
        {

            InitQueueForPut(queue);
            try
            {

                MQMessage mqMsg = new();

                mqMsg.WriteString(message);
                mqMsg.Format = MQC.MQFMT_STRING;


                MQPutMessageOptions mqPutMsgOpts = new();
                mqQueue.Put(mqMsg, mqPutMsgOpts);
                Console.WriteLine(message + " enviada");

                mqQueue?.Close();
                mqQMgr?.Disconnect();
            }
            catch (MQException e)
            {
                Console.Write(e);
                Console.Write(e.Message);
                Console.Write(e.Reason);
                Console.Write(e.StackTrace);
            }
        }

        private bool IsQueueEmpty()
        {
            return mqQueue.CurrentDepth == 0;
        }

        public int GetQueueDepth(string queue)
        {
            InitQueue(queue);
            return mqQueue.CurrentDepth;
        }

        private void InitQueueForPut(string queue)
        {
            if (mqQMgr != null)
            {
                if (mqQueue == null)
                {
                    mqQueue = mqQMgr.AccessQueue(queue, MQC.MQOO_OUTPUT | MQC.MQOO_FAIL_IF_QUIESCING);
                }
                return;
            }
            Hashtable props = new()
    {
        { MQC.HOST_NAME_PROPERTY, Settings.Host },
        { MQC.CHANNEL_PROPERTY, Settings.Channel },
        { MQC.PORT_PROPERTY, Settings.Port },
        { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
        { MQC.USER_ID_PROPERTY, Settings.Username },
        { MQC.PASSWORD_PROPERTY, Settings.Password }
    };
            mqQMgr = new MQQueueManager(Settings.QueueManagerName, props);
            mqQueue = mqQMgr.AccessQueue(queue, MQC.MQOO_OUTPUT | MQC.MQOO_FAIL_IF_QUIESCING);
        }
    }
}
