using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Common
{
    public enum Status
    {
        Online,
        Busy,
        Away,
        Offline,
    }

    public class ChatChannelMember
    {
        public string Id;
        public string Name;
        public Status Status;
    }

    public class ChatMessage
    {
        public DateTime Creation;

        public string Message;

        public string MemberId;
    }

    public interface IChatChannel : Orleans.IGrainWithStringKey
    {
        // Returns channel member id
        Task<string> JoinChannel(string displayName);

        Task LeaveChannel(string callerId);

        Task SendMessage(string callerId, string message);

        Task Whisper(string callerId, string targetId, string message);

        Task<IEnumerable<ChatMessage>> GetHistory();

        Task<IEnumerable<ChatChannelMember>> GetMembers();
    }

    public enum Event
    {
        MemberJoined,
        MemberLeft,
        Message,
        Whisper,
    }

    public class ChatChannelEvent
    {
        public Event EventType;
        
        public string EventData;

        public string MemberId;
    }
}
