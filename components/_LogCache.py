from collections import OrderedDict


class LogCache:
    def __init__(self, capacity=5000):
        self._capacity = max(1, int(capacity))
        self._cache = OrderedDict()

    @property
    def capacity(self):
        return self._capacity

    def clear(self):
        self._cache.clear()

    def has(self, key):
        return key in self._cache

    def get(self, key):
        value = self._cache.get(key)
        if value is None:
            return None
        self._cache.move_to_end(key)
        return value

    def put(self, key, value):
        if key in self._cache:
            self._cache.move_to_end(key)
        self._cache[key] = value
        if len(self._cache) > self._capacity:
            self._cache.popitem(last=False)
