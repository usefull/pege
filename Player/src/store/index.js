import { configureStore, combineReducers } from '@reduxjs/toolkit';
import { persistStore, persistReducer, FLUSH, REHYDRATE, PAUSE, PERSIST, PURGE, REGISTER } from 'redux-persist';
import storage from './storage';

import streamsReducer from '../features/StreamsSlice';
import playerReducer from '../features/PlayerSlice';
import equalizerReducer from '../features/EqualizerSlice';

const equalizerPersistConfig = {
    key: 'equalizer',
    storage
};

const equalizerPersistedReducer = persistReducer(
    equalizerPersistConfig,
    equalizerReducer
);

const playerPersistConfig = {
    key: 'player',
    storage,
    whitelist: ['currentStream'],
};

const playerPersistedReducer = persistReducer(
    playerPersistConfig,
    playerReducer
);

const rootReducer = combineReducers({
    streams: streamsReducer,
    player: playerPersistedReducer,
    equalizer: equalizerPersistedReducer
});

const rootPersistConfig = {
    key: 'root',
    storage,
    version: 1,
    blacklist: ['streams', 'player']
};

const persistedReducer = persistReducer(rootPersistConfig, rootReducer);

export const store = configureStore({
    reducer: persistedReducer,
    middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware({
            serializableCheck: {
                ignoredActions: [FLUSH, REHYDRATE, PAUSE, PERSIST, PURGE, REGISTER],
            },
        }),
});

export const persistor = persistStore(store);