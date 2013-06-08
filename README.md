Differ
=====================

Differ is a _fast_ generic object compare tool.  It's based on `SqlDiffer` from Dapper found [here](http://code.google.com/p/dapper-dot-net/source/browse/Dapper.Rainbow/Snapshotter.cs).

Why?
---------------------
I needed a solution that could do change tracking on "stateless" (different instances) of the same type `T` when persisting (i.e. original vs. modified/current).  During an update, the __original__ value of `T` is pulled from the database and compared with the __current__ object posted to the server.  From here we can make business decisions based on what properties have changed and produce a human-readable audit log.

Stateless?
---------------------
`Snappshotter` works really well for objects are not serialized over the wire.  When dealing with a REST facade and a statless web connection, during a `POST` or `PUT` back do the server, I needed a way to yank out the original object and compare it to the modified one- not simply for SQL generation but also for auditing purposes.

What makes this so special?
---------------------
The real magic is in IL emitting (Specifically `Emit` and `CreateDelegate` methods). [@SamSaffron](https://github.com/SamSaffron), the original creator of [Dapper](http://code.google.com/p/dapper-dot-net/) (and `Snapshotter`), explains at a high level how this paid dividends for Stack Overflow [here](http://samsaffron.com/archive/2011/03/30/How+I+learned+to+stop+worrying+and+write+my+own+ORM).  

TLDR: You don't pay a reflection penalty for reading object properties after the first comparison of any given `T`; the "Differ" method is cached as a `Func`.

Code Already
---------------------
With a little help from our friend [AutoFixture](https://github.com/AutoFixture/AutoFixture) we can fill our `SimpleType` "object under test" with mock changes:
```csharp
//in the real world, we'd be comparing the updated value with what's currently in the repository
var @out = fixture.CreateAnonymous<SimpleType>();
var outChanged = fixture.CreateAnonymous<SimpleType>();

var changes = Differ<SimpleType>.Diff(@out, outChanged).ToList();
```
That's it.

There's also a small helper method for creating more human-friendly messages useful for logging to an audit log (given `changes` from above):  
```csharp
var auditText = diffs.ToFriendlyDescription();
```
which produces something simiar to the following:
```
SimpleType had the following properties changed:
SomeProp: 0 -> 1
```

Limitations
---------------------
Unfortunately, this code will not cure cancer or stupidity.  Though pull requests are welcome.

It also won't compare "sub types"- that is, given:
```csharp
public class SimpleType
{
	public int SomeProp { get; set; }
}
public class ComplexType
{
	public SimpleType SimpleTypeProp { get; set; }
}
```
... it won't compare `SimpleTypeProp`.  This would require some recursive diffing and I haven't encounted a need for it yet (of course contributions are welcome :smiley:).

Similar Projects
---------------------
- [Dapper](http://code.google.com/p/dapper-dot-net/) (specifically, [Snapshotter](http://code.google.com/p/dapper-dot-net/source/browse/Dapper.Rainbow/Snapshotter.cs) mentioned earlier).
- [Bookkeeper](https://github.com/Iristyle/BookKeeper) by [@iristyle](https://github.com/iristyle)  

There may be others... if you know of one, create an issue and I'll list it here.
