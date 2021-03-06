﻿using GUI.NetCommunication.MessageTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GUI.NetCommunication
{
    public class MessageContainer
    {
        public MessageContainer()
        {
            Messages = new List<Message>();
        }

        public MessageContainer(List<Message> messages)
        {
            Messages = messages;
        }

        public MessageContainer(Message msg)
        {
            Messages = new List<Message>() { msg };
        }

        [XmlElement(typeof(TextMessage), ElementName = "TextMessage")]
        [XmlElement(typeof(DrawData), ElementName = "DrawData")]
        [XmlElement(typeof(DrawClear), ElementName = "DrawClear")]
        [XmlElement(typeof(DrawLock), ElementName = "DrawLock")]
        [XmlElement(typeof(DrawUnlock), ElementName = "DrawUnlock")]
        [XmlElement(typeof(UserCount), ElementName = "UserCount")]
        [XmlElement(typeof(ClientDisconnect), ElementName = "ClientDisconnect")]
        [XmlElement(typeof(ServerDisconnect), ElementName = "ServerDisconnect")]
        [XmlElement(typeof(DrawDataBlockFlag), ElementName = "DrawDataBlockFlag")]
        [XmlElement(typeof(DrawDataBlock), ElementName = "DrawDataBlock")]        
        public List<Message> Messages { get; set; }
    }
}
