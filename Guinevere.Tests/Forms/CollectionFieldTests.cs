using System.Collections.ObjectModel;
using Guinevere.Forms;

namespace Guinevere.Tests.Forms;

public class CollectionFieldTests
{
    [Fact]
    public void ListExposesIndexedEntries()
    {
        var target = new Model();
        var collection = Collection(target, nameof(Model.List));

        Assert.False(collection.IsDictionary);
        Assert.Equal(typeof(int), collection.ElementType);
        Assert.Equal(3, collection.Count);
        Assert.Equal("List", collection.Label);
        Assert.True(collection.CanResize);

        var entries = collection.Entries();
        Assert.Equal(["[0]", "[1]", "[2]"], entries.Select(entry => entry.Name));
        Assert.Equal([1, 2, 3], entries.Select(entry => (int)entry.GetValue()!));
        Assert.All(entries, entry => Assert.Equal(nameof(Model.List), entry.CollectionMember));
        Assert.Equal(2, entries[2].CollectionIndex);
        Assert.Same(target, entries[0].Target);
    }

    [Fact]
    public void ListEntryWriteStoresValueAndNotifies()
    {
        var target = new Model();
        var notified = new List<object>();
        var collection = Collection(target, nameof(Model.List), new FormOptions { MutationNotifier = notified.Add });

        Assert.True(collection.Entries()[1].SetValue(20));

        Assert.Equal([1, 20, 3], target.List);
        Assert.Equal([target], notified);
    }

    [Fact]
    public void EntryTouchNotifiesOwner()
    {
        var target = new Model();
        var notified = new List<object>();
        var collection = Collection(target, nameof(Model.List), new FormOptions { MutationNotifier = notified.Add });

        collection.Entries()[0].Touch();

        Assert.Equal([target], notified);
    }

    [Fact]
    public void StaleListEntryReadsNullAndRefusesWrites()
    {
        var target = new Model();
        var collection = Collection(target, nameof(Model.List));
        var last = collection.Entries()[2];

        Assert.True(collection.RemoveAt(2));

        Assert.Null(last.GetValue());
        Assert.False(last.SetValue(9));
        Assert.Equal([1, 2], target.List);
    }

    [Fact]
    public void ListAddAppendsDefaultAndNotifies()
    {
        var target = new Model();
        var notified = new List<object>();
        var options = new FormOptions { MutationNotifier = notified.Add };

        Assert.True(Collection(target, nameof(Model.List), options).Add());
        Assert.True(Collection(target, nameof(Model.Strings), options).Add());
        Assert.True(Collection(target, nameof(Model.Items), options).Add());
        Assert.True(Collection(target, nameof(Model.Abstracts), options).Add());

        Assert.Equal([1, 2, 3, 0], target.List);
        Assert.Equal("", Assert.Single(target.Strings));
        Assert.NotNull(Assert.Single(target.Items));
        Assert.Null(Assert.Single(target.Abstracts));
        Assert.Equal(4, notified.Count);
    }

    [Fact]
    public void RemoveAtRejectsOutOfRangeIndices()
    {
        var collection = Collection(new Model(), nameof(Model.List));

        Assert.False(collection.RemoveAt(-1));
        Assert.False(collection.RemoveAt(3));
        Assert.Equal(3, collection.Count);
    }

    [Fact]
    public void ArrayEntriesAreWritableButArrayIsNotResizable()
    {
        var target = new Model();
        var collection = Collection(target, nameof(Model.Array));

        Assert.Equal(typeof(string), collection.ElementType);
        Assert.False(collection.IsReadOnly);
        Assert.False(collection.CanResize);
        Assert.False(collection.Add());
        Assert.False(collection.RemoveAt(0));

        Assert.True(collection.Entries()[0].SetValue("z"));
        Assert.Equal(["z", "b"], target.Array);
    }

    [Fact]
    public void DictionaryExposesKeyedEntries()
    {
        var target = new Model();
        var collection = Collection(target, nameof(Model.ByName));

        Assert.True(collection.IsDictionary);
        Assert.Equal(typeof(int), collection.ElementType);
        Assert.Equal(2, collection.Count);

        var entries = collection.Entries();
        Assert.Equal(["a", "b"], entries.Select(entry => entry.Name));
        Assert.Equal(2, entries[1].GetValue());
        Assert.Null(entries[0].CollectionMember);
    }

    [Fact]
    public void DictionaryEntryWriteStoresValueAndNotifies()
    {
        var target = new Model();
        var notified = new List<object>();
        var collection = Collection(target, nameof(Model.ByName), new FormOptions { MutationNotifier = notified.Add });

        Assert.True(collection.Entries()[0].SetValue(10));

        Assert.Equal(10, target.ByName["a"]);
        Assert.Equal([target], notified);
    }

    [Fact]
    public void RemovedDictionaryEntryReadsNull()
    {
        var target = new Model();
        var collection = Collection(target, nameof(Model.ByName));
        var first = collection.Entries()[0];

        Assert.True(collection.RemoveAt(0));

        Assert.Null(first.GetValue());
        Assert.Equal(["b"], target.ByName.Keys);
    }

    [Fact]
    public void DictionaryAddInventsStringKeys()
    {
        var target = new Model();
        var collection = Collection(target, nameof(Model.ByName));

        Assert.True(collection.Add());
        Assert.True(collection.Add());

        Assert.Equal(0, target.ByName["New Key"]);
        Assert.Equal(0, target.ByName["New Key 1"]);
    }

    [Fact]
    public void DictionaryAddInventsIntAndLongKeys()
    {
        var target = new Model();
        target.ById[0] = "taken";

        Assert.True(Collection(target, nameof(Model.ById)).Add());
        Assert.True(Collection(target, nameof(Model.ByLong)).Add());

        Assert.Equal("", target.ById[1]);
        Assert.Equal(0f, target.ByLong[0L]);
    }

    [Fact]
    public void DictionaryWithUninventableKeyIsNotResizable()
    {
        var collection = Collection(new Model(), nameof(Model.ByGuid));

        Assert.False(collection.CanResize);
        Assert.False(collection.Add());
    }

    [Fact]
    public void ReadOnlyFormPropagatesIntoEntries()
    {
        var target = new Model();
        var options = new FormOptions { ReadOnly = true };
        var list = Collection(target, nameof(Model.List), options);
        var map = Collection(target, nameof(Model.ByName), options);

        Assert.True(list.IsReadOnly);
        Assert.False(list.CanResize);
        Assert.False(list.Add());
        Assert.False(list.RemoveAt(0));
        Assert.All(list.Entries(), entry => Assert.True(entry.IsReadOnly));
        Assert.False(list.Entries()[0].SetValue(9));
        Assert.False(map.Entries()[0].SetValue(9));

        Assert.Equal([1, 2, 3], target.List);
        Assert.Equal(1, target.ByName["a"]);
    }

    [Fact]
    public void ReadOnlyCollectionInstanceIsReadOnly()
    {
        var collection = Collection(new Model(), nameof(Model.Frozen));

        Assert.True(collection.IsReadOnly);
        Assert.False(collection.Entries()[0].SetValue(5));
    }

    [Fact]
    public void StringIsNotACollection()
    {
        var field = Field(new Model(), nameof(Model.Text));

        Assert.Null(CollectionField.TryCreate(field));
        Assert.False(CollectionField.IsCollection(field));
    }

    [Fact]
    public void NonCollectionValuesAreNotCollections()
    {
        var target = new Model();

        Assert.False(CollectionField.IsCollection(Field(target, nameof(Model.Number))));
        Assert.False(CollectionField.IsCollection(Field(target, nameof(Model.Missing))));
        Assert.True(CollectionField.IsCollection(Field(target, nameof(Model.List))));
        Assert.Throws<ArgumentNullException>(() => CollectionField.TryCreate(null!));
    }

    [Fact]
    public void NonGenericListFallsBackToObjectElements()
    {
        var collection = Collection(new Model(), nameof(Model.Untyped));

        Assert.Equal(typeof(object), collection.ElementType);
        Assert.True(collection.Add());
        Assert.IsType<object>(collection.Entries()[1].GetValue());
    }

    static FormField Field(Model target, string name, FormOptions? options = null) =>
        FormField.ForMember(typeof(Model).GetField(name)!, target, options);

    static CollectionField Collection(Model target, string name, FormOptions? options = null) =>
        CollectionField.TryCreate(Field(target, name, options))!;

    abstract class Base;

    sealed class Item;

    sealed class Model
    {
        public List<int> List = [1, 2, 3];
        public List<string> Strings = [];
        public List<Item> Items = [];
        public List<Base> Abstracts = [];
        public string[] Array = ["a", "b"];
        public Dictionary<string, int> ByName = new() { ["a"] = 1, ["b"] = 2 };
        public Dictionary<int, string> ById = [];
        public Dictionary<long, float> ByLong = [];
        public Dictionary<Guid, int> ByGuid = [];
        public ReadOnlyCollection<int> Frozen = new([1]);
        public System.Collections.ArrayList Untyped = [1];
        public string Text = "abc";
        public int Number = 1;
        public List<int>? Missing = null;
    }
}
