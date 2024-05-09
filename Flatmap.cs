using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
public class Flatmap<TKey, TValue>
{
    internal enum ItemState : byte {
        Empty = 0,
        Occupied = 1,
        Deleted = 2
    }
    internal uint _size;
    internal uint _capacity;
    internal bool _using_ptrs;
    internal struct KVS {
        public TKey Key { get; set; }
        public TValue Value { get; set; }
        public ItemState State { get; set; }
    }
    internal TKey[] _keys;

    internal KVS<TKey, TValue, ItemState>[]? _data;
    internal GCHandle[]? _ptrs;
    public Flatmap(uint capacity = 10) {
        _size = 0;
        _capacity = capacity;
        unsafe {
            if (sizeof(KVS<TKey, TValue, ItemState>) > 16) {
                _ptrs = new GCHandle[_capacity];
                _using_ptrs = true;
            } else {
                _data = new KVS<TKey, TValue, ItemState>[_capacity];
                _using_ptrs = false;
            }
        }
        _keys = new TKey[_capacity];
    }
    ~Flatmap() {
        if (!_using_ptrs) {
            return;
        }
        for (uint i = 0; i < _size; i++) {
            int free_idx = findIndex(_keys[i], true);
            if (free_idx == -1) {
                throw new Exception("Failed to find (previously inserted) key in hashmap, should never happen");
            }
            _ptrs[i].Free();
        }
    }

    private void resize() {
        Console.WriteLine("resizing!");
        Flatmap<TKey, TValue> new_map = new Flatmap<TKey, TValue>(_capacity * 2);
        for (uint i = 0; i < _size; i++) {
            // if deleted, don't add to new array
            int search = findIndex(_keys[i], false);
            if (search != -1) {
                TValue val = _using_ptrs ? ((KVS<TKey, TValue, ItemState>) _ptrs[search].Target).Value : _data[search].Value;
                new_map.Add(_keys[i], val);
            }
        }
        _size = new_map._size;
        _capacity = new_map._capacity;
        _keys = new_map._keys;
        if (_using_ptrs) {
            _ptrs = new_map._ptrs; 
        } else {
            _data = new_map._data;
        }
    }

    public TValue this[TKey key] {
        get { return this.Get(key); }
        set { this.Add(key, value); }
    }

    public void Add(TKey key, TValue val) {
        // use quadratic probign
        // index = (hash + i^2) % table_size, i = 0 ... until found
        if (_size >= _capacity / 10 * 8) {
            resize();
        }
        uint hash = (uint) HashCode.Combine(key);
        uint index = hash % _capacity;
        uint i = 1;
        if (_using_ptrs) {
            while (_ptrs[index].IsAllocated) { 
                // grab ele? 
                KVS<TKey, TValue, ItemState> inner_ele = (KVS<TKey, TValue, ItemState>) _ptrs[index].Target;
                if (key?.Equals(inner_ele.Key) == true) {
                    inner_ele.Value = val;
                    inner_ele.State = ItemState.Occupied;
                    _ptrs[index].Target = inner_ele;
                    return;
                }
                index = (hash + i * i) % _capacity;
                i++;
            }
            KVS<TKey, TValue, ItemState> ele = default;
            ele.Key = key;
            ele.Value = val;
            ele.State = ItemState.Occupied;
            GCHandle temp = GCHandle.Alloc(ele, GCHandleType.Normal);
            _ptrs[index] = temp;
        } else {
            while (_data[index].State != ItemState.Empty) {
                // can re-use deleted slots too as well as over-write existing
                if (key?.Equals(_data[index].Key) == true) {
                    // key already exists, replace value
                    _data[index].Value = val;
                    _data[index].State = ItemState.Occupied;
                    return;
                }
                index = (hash + i * i) % _capacity;
                i++;
            }
            _data[index].Key = key;
            _data[index].Value = val;
            _data[index].State = ItemState.Occupied;
        }
        // if no early return (i.e. key ADDED, not replaced), increment size + add keys to list of keys
        _keys[_size] = key;
        _size++;
    }

    private int findIndex(TKey key, bool include_deleted) {
        uint hash = (uint) HashCode.Combine(key);
        uint index = hash % _capacity;
        uint i = 1;
        if (_using_ptrs) {
            while (_ptrs[index].IsAllocated) {
                KVS<TKey, TValue, ItemState> ele = (KVS<TKey, TValue, ItemState>) _ptrs[index].Target;
                if (key?.Equals(ele.Key) == true && (ele.State == ItemState.Occupied || include_deleted)) {
                    // found value inside map
                    return (int) index;
                }
                index = (hash + i * i) % _capacity;
                i++;
            }
        } else {
            while (_data[index].State == ItemState.Occupied || (_data[index].State == ItemState.Deleted && include_deleted)) {
                if (key?.Equals(_data[index].Key) == true) {
                    return (int) index;
                }
                index = (hash + i * i) % _capacity;
                i++;
            }
        }
        // no such key
        return -1;
    }

    public bool Contains(TKey key) {
        return findIndex(key, false) != -1;
    }

    public TValue Get(TKey key) {
        int search = findIndex(key, false);
        if (search == -1) {
            // fell through, didn't find it
            return default(TValue);
        } else if (_using_ptrs) {
            KVS<TKey, TValue, ItemState> ele = (KVS<TKey, TValue, ItemState>) _ptrs[search].Target;
            return ele.Value;
        } else{
            return _data[search].Value;
        }
    }

    public bool Remove(TKey key) {

        int search = findIndex(key, false);
        if (search == -1) {
            // fell through, didn't find it
            return false;
        } else if (_using_ptrs) {
            // key exists, could recycle "deleted" too
            KVS<TKey, TValue, ItemState> ele = (KVS<TKey, TValue, ItemState>) _ptrs[search].Target;
            ele.State = ItemState.Deleted;
            _ptrs[search].Target = ele;
            return true;
        } else {
            _data[search].State = ItemState.Deleted;
            return true;
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        for (uint i = 0; i < _size; i++) {
            // if deleted, don't add to new array
            TKey key = _keys[i];
            int search = findIndex(key, false);
            if (search != -1) {
                TValue val = _using_ptrs ? ((KVS<TKey, TValue, ItemState>) _ptrs[search].Target).Value : _data[search].Value;
                sb.Append(" ");
                sb.Append(key is string ? $"\'{key}\'" : key.ToString());
                sb.Append(": ");
                sb.Append(val is string ? $"\'{val}\'" : val.ToString());
                sb.Append(",");
            }
        }
        // trim last comma, iff hashmap is not empty
        if (sb.Length > 2) {
            sb.Remove(sb.Length - 1, 1);
            sb.Append(" ");
        }
        sb.Append("}");
        return sb.ToString();
    }
}

