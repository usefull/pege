import { useState } from 'react';

function useLocalStorage(key, initialValue) {
  // Состояние для хранения значения
  const [storedValue, setStoredValue] = useState(() => {
    try {
      // Получаем из localStorage
      const item = window.localStorage.getItem(key);
      // Если есть - парсим, иначе возвращаем initialValue
      return item ? JSON.parse(item) : initialValue;
    } catch (error) {
      console.error('Error reading localStorage:', error);
      return initialValue;
    }
  });

  // Функция для обновления значения
  const setValue = (value) => {
    try {
      // Разрешаем передавать функцию как в useState
      const valueToStore = value instanceof Function ? value(storedValue) : value;
      
      // Сохраняем в состояние
      setStoredValue(valueToStore);
      
      // Сохраняем в localStorage
      window.localStorage.setItem(key, JSON.stringify(valueToStore));
    } catch (error) {
      console.error('Error writing to localStorage:', error);
    }
  };

  return [storedValue, setValue];
}

export default useLocalStorage;