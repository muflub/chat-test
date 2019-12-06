using Common;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Orleans.Streams;
using Orleans.Core;
using Orleans.Runtime;

namespace ChatServer
{
    class ChatChannelImpl : Orleans.Grain, IChatChannel
    {
        string _name;
        IStreamProvider _streamProvider;

        List<ChatMessage> _history = new List<ChatMessage>();
        Dictionary<string, ChatChannelMember> _members = new Dictionary<string, ChatChannelMember>();
        Dictionary<string, IAsyncStream<ChatChannelEvent>> _memberStreams = new Dictionary<string, IAsyncStream<ChatChannelEvent>>();

        public ChatChannelImpl()
        {

        }

        public override Task OnActivateAsync()
        {
            _name = this.GrainReference.GrainIdentity.PrimaryKeyString;
            _streamProvider = GetStreamProvider("SMSProvider");
            return base.OnActivateAsync();
        }

        public Task<IEnumerable<ChatMessage>> GetHistory()
        {
            IEnumerable<ChatMessage> res = _history;
            return Task.FromResult(res);
        }

        public Task<IEnumerable<ChatChannelMember>> GetMembers()
        {
            return Task.FromResult(_members.Select(a => a.Value));
        }

        private Task SendEvent(ChatChannelEvent e)
        {
            var tasks = new List<Task>();

            foreach (var m in _memberStreams)
            {
                tasks.Add(m.Value.OnNextAsync(e));
            }

            return Task.WhenAll(tasks);
        }

        public Task<string> JoinChannel(string displayName)
        {
            var id = Guid.NewGuid();
            RequestContext.Set("memberId", id.ToString());

            _members.Add(id.ToString(), new ChatChannelMember
            {
                Id = id.ToString(),
                Name = displayName,
                Status = Status.Online,
            });

            var eventTask = SendEvent(new ChatChannelEvent()
            {
                EventType = Event.MemberJoined,
                EventData = displayName,
                MemberId = id.ToString(),
            });

            _memberStreams.Add(id.ToString(), _streamProvider.GetStream<ChatChannelEvent>(id, _name));
            return eventTask.ContinueWith(a => id.ToString());
        }

        public Task LeaveChannel(string callerId)
        {
            if (_members.ContainsKey(callerId) == false)
            {
                return Task.CompletedTask;
            }

            _memberStreams[callerId].OnCompletedAsync();

            _members.Remove(callerId);
            _memberStreams.Remove(callerId);

            return SendEvent(new ChatChannelEvent()
            {
                EventType = Event.MemberLeft,
                MemberId = callerId,
            });
        }

        public Task SendMessage(string callerId, string message)
        {
            return SendEvent(new ChatChannelEvent()
            {
                EventType = Event.Message,
                EventData = message,
                MemberId = callerId,
            });
        }

        public Task Whisper(string callerId, string targetId, string message)
        {
            IAsyncStream<ChatChannelEvent> stream;
            if (_memberStreams.TryGetValue(targetId, out stream))
            {
                return stream.OnNextAsync(new ChatChannelEvent()
                {
                    EventType = Event.Whisper,
                    EventData = message,
                    MemberId = callerId,
                });
            }

            return Task.CompletedTask;
        }
    }
}
