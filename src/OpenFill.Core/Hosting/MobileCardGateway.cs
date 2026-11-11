using System.Text.Json.Nodes;
namespace OpenFill.Core.Hosting;
public sealed record MobileCard(string InstanceId,string CardId,string TaskKey,string Kind,string Question,string[] Options,DateTimeOffset OpenedAtUtc,DateTimeOffset ExpiresAtUtc);
public sealed record MobileCardEvent(long Sequence,string EventId,string Kind,MobileCard Card,string? Outcome);
public sealed record MobileAnswer(string ActionId,string InstanceId,string CardId,string TaskKey,string? Text);
public sealed record MobileAnswerResult(string Status,bool Accepted,bool Duplicate=false);
public sealed class MobileCardGateway
{
 private readonly object _gate=new();
 private readonly Dictionary<string,MobileCard> _cards=new(StringComparer.Ordinal);
 private readonly Dictionary<string,MobileAnswerResult> _actions=new(StringComparer.Ordinal);
 private readonly List<MobileCardEvent> _events=new();
 private readonly Func<string,string,bool> _accept;
 private long _sequence;
 public string InstanceId {get;}=Guid.NewGuid().ToString("N");
 public MobileCardGateway(Func<string,string,bool> accept)=>_accept=accept;
 public MobileCard[] OpenCards(){lock(_gate)return _cards.Values.ToArray();}
 public MobileCardEvent[] ReadEvents(long after){lock(_gate)return _events.Where(x=>x.Sequence>after).ToArray();}
 public long LastSequence{get{lock(_gate)return _sequence;}}
 public void Open(JsonObject card,string? taskKey,TimeSpan timeout)
 {
  var kind=card["kind"]?.GetValue<string>()??"";
  if(kind!="confirm"&&(kind!="ask"||card["human"]?.GetValue<bool>()!=true))return;
  var id=card["id"]?.GetValue<string>();if(string.IsNullOrEmpty(id))return;
  var now=DateTimeOffset.UtcNow;
  lock(_gate){
   var c=new MobileCard(InstanceId,id,taskKey??"",kind,card["question"]?.GetValue<string>()??card["what"]?.GetValue<string>()??"",
    card["options"]?.AsArray().Select(x=>x?.ToString()??"").ToArray()??Array.Empty<string>(),now,now.Add(timeout));
   _cards[id]=c;_events.Add(new(++_sequence,Guid.NewGuid().ToString("N"),"opened",c,null));
  }
 }
 public void Close(string id,string outcome){lock(_gate)if(_cards.Remove(id,out var c))_events.Add(new(++_sequence,Guid.NewGuid().ToString("N"),"closed",c,outcome));}
 public MobileAnswerResult Submit(MobileAnswer a)
 {
  lock(_gate){
   if(_actions.TryGetValue(a.ActionId,out var old))return old with{Duplicate=true};
   MobileAnswerResult r;
   if(a.InstanceId!=InstanceId)r=new("wrong_instance",false);
   else if(!_cards.TryGetValue(a.CardId,out var c))r=new("not_found",false);
   else if(c.TaskKey!=a.TaskKey)r=new("wrong_task",false);
   else if(DateTimeOffset.UtcNow>=c.ExpiresAtUtc)r=new("expired",false);
   else if(a.Text is null||a.Text.Length>16000)r=new("invalid",false);
   else if(c.Kind=="confirm"&&a.Text!="yes"&&a.Text!="no"&&!a.Text.StartsWith("yes\n",StringComparison.Ordinal)&&!a.Text.StartsWith("no\n",StringComparison.Ordinal))r=new("invalid",false);
   else r=_accept(a.CardId,a.Text)?new("accepted",true):new("already_answered",false);
   _actions[a.ActionId]=r;return r;
  }
 }
}
