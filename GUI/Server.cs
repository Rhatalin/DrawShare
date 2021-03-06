﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;
using GUI.NetCommunication;
using GUI.NetCommunication.MessageTypes;
using System.Windows.Media;
using static GUI.Enums;

namespace GUI
{
    public class Server
    {
        #region Constructors

        public Server(MainWindow mainwindow)
        {
            mw = mainwindow;
            MessageReceived += mw.MessageReceived;
        }

        #endregion Constructors

        #region Attributes and Variables

        public TcpListener Listener { get; set; }
        public List<Transfer<MessageContainer>> Transfers { get; set; } = new List<Transfer<MessageContainer>>();
        private readonly object __lockTransfers = new object();
        public event MessageReceivedEventHandler MessageReceived;
        private MainWindow mw;

        #endregion Attributes and Variables

        #region Methodes

        public bool TryPort(int port)
        {
            try
            {
                Listener = new TcpListener(IPAddress.Any, port);
                Listener.Start();
            }
            catch (SocketException)
            {
                mw.WriteDebug("SocketException in Server.TryPort", LogLevels.NoLog);
                return false;
            }
            return true;
        }

        public void Receive()
        {
            try
            {
                while (true)
                {
                    Transfer<MessageContainer> transfer = new Transfer<MessageContainer>(Listener.AcceptTcpClient());
                    mw.WriteDebug("Client connected", LogLevels.Info);
                    int transferCount;
                    lock (__lockTransfers)
                        Transfers.Add(transfer);
                    transferCount = Transfers.Count + 1;

                    mw.UseDispatcher(mw.Canvas_Drawing, delegate
                    {
                        var msgList = new List<Message>();

                        if (mw.Canvas_Drawing.Children.Count < 1000) // Init with normal DrawData
                        {
                            //Send Drawing Data

                            foreach (var el in mw.Canvas_Drawing.Children)
                            {
                                if (el as Line != null)
                                {
                                    Line l = (Line)el;
                                    msgList.Add(new DrawData(l.X1, l.Y1, l.X2, l.Y2, (double)l.GetValue(Shape.StrokeThicknessProperty), l.GetValue(Shape.StrokeProperty).ToString()));
                                }
                            }

                            if (mw.DrawingLocked)
                                msgList.Add(new DrawLock());
                            else
                                msgList.Add(new DrawUnlock());

                            Send(transfer, new MessageContainer(msgList));

                            //Update other users
                            SendAll(new UserCount(transferCount));
                        }
                        else // Init with async DrawDataBlock
                        {
                            var msgListDrawData = new List<Message>();
                            msgList.Add(new DrawDataBlockFlag());

                            if (mw.DrawingLocked)
                                msgList.Add(new DrawLock());
                            //send no DrawUnlock to ensure the board is locked until DrawDataBlock

                            Send(transfer, new MessageContainer(msgList));

                            //Update other users
                            SendAll(new UserCount(transferCount));

                            //Send DrawDataBlock
                            CustomBrush lastBrush = null;
                            var lastLines = new List<NetCommunication.MessageTypes.SupportClasses.Line>();
                            foreach (var el in mw.Canvas_Drawing.Children)
                            {
                                if (el as Line != null)
                                {
                                    Line l = (Line)el;
                                    if (lastBrush == null)
                                        lastBrush = new CustomBrush((SolidColorBrush)l.Stroke, l.StrokeThickness);
                                    if (l.StrokeThickness != lastBrush.Thickness || l.Stroke != lastBrush.ColorBrush)
                                    {
                                        msgListDrawData.Add(new DrawDataBlock(lastBrush.ColorBrush.ToString(), lastBrush.Thickness, new List<NetCommunication.MessageTypes.SupportClasses.Line>(lastLines)));
                                        lastLines.Clear();
                                        lastBrush = new CustomBrush((SolidColorBrush)l.Stroke, l.StrokeThickness);
                                    }
                                    lastLines.Add(new NetCommunication.MessageTypes.SupportClasses.Line(l.X1, l.Y1, l.X2, l.Y2));
                                }
                            }
                            msgListDrawData.Add(new DrawDataBlock(lastBrush.ColorBrush.ToString(), lastBrush.Thickness, new List<NetCommunication.MessageTypes.SupportClasses.Line>(lastLines)));

                            Send(transfer, new MessageContainer(msgListDrawData));
                        }
                    });

                    //Start Receiver-Thread
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            while (true)
                            {
                                MessageReceived?.Invoke(transfer, new MessageReceivedEventArgs(transfer.Receive()));
                            }
                        }
                        catch (IOException)
                        {
                            OnClientDisconnect(transfer);
                            mw.WriteDebug("IOException in Server.Receive in transfer.Receive", LogLevels.Debug);
                        }
                    });
                    mw.UseDispatcher(mw.TBl_UserCount, delegate { mw.TBl_UserCount.Text = (transferCount).ToString(); });
                }
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode != 10004) //WSACancelBlockingCall
                {
                    mw.WriteDebug("WSACancelBlockingCall in Server.Recieve", LogLevels.Debug);
                    throw ex;
                }
            }
        }

        public void Send(Transfer<MessageContainer> transfer, MessageContainer msgc)
        {
            try
            {
                transfer.Send(msgc);
            }
            catch (IOException)
            {
                OnClientDisconnect(transfer);
                mw.WriteDebug("IOException in Server.Send", LogLevels.Debug);
            }
        }

        public void Send(Transfer<MessageContainer> transfer, Message msg)
        {
            Send(transfer, new MessageContainer(msg));
        }

        public void SendAll(MessageContainer msgc)
        {
            foreach (var t in Transfers)
            {
                Send(t, msgc);
            }
        }

        public void SendAll(Message msg)
        {
            SendAll(new MessageContainer(msg));
        }

        public void Stop()
        {
            Listener.Stop();
            lock (__lockTransfers)
            {
                for (int i = 0; i < Transfers.Count; i++)
                {
                    var t = Transfers[i];
                    Send(t, new MessageContainer(new ServerDisconnect()));
                    t.TcpClient.Close();
                }
                Transfers.Clear();
            }
        }

        public void OnClientDisconnect(Transfer<MessageContainer> t)
        {
            mw.WriteDebug("Client disconnected", LogLevels.Info);
            lock (__lockTransfers)
            {
                Transfers.Remove(t);
            }
            SendAll(new UserCount(Transfers.Count + 1));
            mw.UserCount = Transfers.Count + 1;
        }

        #endregion Methodes
    }
}
