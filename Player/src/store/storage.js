const storage = {
    getItem: (key) => {
        return Promise.resolve(localStorage.getItem(key));
    },
    setItem: (key, value) => {
        localStorage.setItem(key, value);
        return Promise.resolve(value);
    },
    removeItem: (key) => {
        localStorage.removeItem(key);
        return Promise.resolve();
    },
};

export default storage;