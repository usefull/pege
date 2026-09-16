import { configureStore, combineReducers } from '@reduxjs/toolkit';
import { persistStore, persistReducer, FLUSH, REHYDRATE, PAUSE, PERSIST, PURGE, REGISTER } from 'redux-persist';
import storage from './storage';

import streamsReducer from '../features/StreamsSlice';
import playerReducer from '../features/PlayerSlice';

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
});

const rootPersistConfig = {
    key: 'root',
    storage,
    version: 1,
    blacklist: ['streams']
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