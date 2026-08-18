namespace Pege.Test.Core
{
    public class StreamingMedian
    {
        private readonly double _p; // Целевой квантиль (например, 0.5 для медианы или 0.99 для p99)
        private long _count = 0;

        // Высоты 5 маркеров (оценки квантилей)
        private readonly double[] _q = new double[5];
        // Текущие фактические позиции маркеров (1-based)
        private readonly double[] _n = new double[5];
        // Идеальные (целевые) позиции маркеров
        private readonly double[] _nDesired = new double[5];

        /// <summary>
        /// Возвращает текущее приближенное значение квантиля (медианы)
        /// </summary>
        public double Value => _count < 5 ? GetExactMedianBefore5() : _q[2];

        /// <summary>
        /// Инициализация трекера квантиля.
        /// </summary>
        /// <param name="targetQuantile">0.5 для медианы, 0.95 или 0.99 для перцентилей задержек.</param>
        public StreamingMedian(double targetQuantile = 0.5)
        {
            if (targetQuantile <= 0 || targetQuantile >= 1)
                throw new ArgumentOutOfRangeException(nameof(targetQuantile), "Квантиль должен быть строго между 0 и 1");

            _p = targetQuantile;
        }

        /// <summary>
        /// Добавление нового значения в поток (O(1) по памяти и времени)
        /// </summary>
        public void Update(double value)
        {
            _count++;

            // Первые 5 элементов просто собираем в массив для инициализации маркеров
            if (_count <= 5)
            {
                _q[(int)_count - 1] = value;
                if (_count == 5)
                {
                    Array.Sort(_q);

                    // Начальные позиции маркеров (1, 2, 3, 4, 5)
                    for (int i = 0; i < 5; i++)
                        _n[i] = i + 1;

                    // Настройка идеальных позиций по формуле P-Square под конкретный квантиль
                    _nDesired[0] = 1;
                    _nDesired[1] = 1 + 2 * _p;
                    _nDesired[2] = 1 + 4 * _p; // Идеальная позиция искомого квантиля (индекс 2)
                    _nDesired[3] = 1 + 6 * _p;
                    _nDesired[4] = 5;
                }
                return;
            }

            // 1. Определяем, в какой из 4-х интервалов между маркерами попало новое значение
            int k;
            if (value < _q[0])
            {
                _q[0] = value; // Обновляем абсолютный минимум
                k = 0;
            }
            else if (value < _q[1]) k = 0;
            else if (value < _q[2]) k = 1;
            else if (value < _q[3]) k = 2;
            else if (value <= _q[4]) k = 3;
            else
            {
                _q[4] = value; // Обновляем абсолютный максимум
                k = 3;
            }

            // Сдвигаем фактические позиции всех маркеров, которые находятся правее точки вставки
            for (int i = k + 1; i < 5; i++)
                _n[i]++;

            // Пересчитываем идеальные целевые позиции маркеров для увеличившейся выборки
            _nDesired[0] = 1;
            _nDesired[1] = 1 + (_count - 1) * (_p / 2.0);
            _nDesired[2] = 1 + (_count - 1) * _p;
            _nDesired[3] = 1 + (_count - 1) * ((1.0 + _p) / 2.0);
            _nDesired[4] = _count;

            // 2. Корректируем высоты внутренних маркеров (индексы 1, 2, 3), если они отстают от идеала
            for (int i = 1; i <= 3; i++)
            {
                double d = _nDesired[i] - _n[i];

                // Если маркер сместился относительно идеальной позиции больше чем на 1 шаг в любую сторону
                if ((d >= 1 && _n[i + 1] - _n[i] > 1) || (d <= -1 && _n[i - 1] - _n[i] < -1))
                {
                    int sign = d > 0 ? 1 : -1;

                    // Кусочно-параболическая формула предсказания новой высоты маркера
                    double qP = _q[i] + (sign / (_n[i + 1] - _n[i - 1])) *
                        ((_n[i] - _n[i - 1] + sign) * (_q[i + 1] - _q[i]) / (_n[i + 1] - _n[i]) +
                         (_n[i + 1] - _n[i] - sign) * (_q[i] - _q[i - 1]) / (_n[i] - _n[i - 1]));

                    // Проверяем математическое условие: маркер обязан оставаться между своими соседями
                    if (_q[i - 1] < qP && qP < _q[i + 1])
                    {
                        _q[i] = qP; // Применяем параболическое сглаживание
                    }
                    else
                    {
                        // Если парабола дает аномальный скачок, используем строго линейную интерполяцию
                        _q[i] = _q[i] + sign * (_q[i + sign] - _q[i]) / (_n[i + sign] - _n[i]);
                    }

                    // Фиксируем сдвиг маркера на 1 шаг в сторону идеала
                    _n[i] += sign;
                }
            }
        }

        /// <summary>
        /// Точный расчет, пока не набралось 5 элементов для старта аппроксимации
        /// </summary>
        private double GetExactMedianBefore5()
        {
            if (_count == 0) return 0;

            var temp = _q.Take((int)_count).OrderBy(x => x).ToArray();
            int len = temp.Length;

            // Ищем точный квантиль методом ближайшего ранга для маленькой выборки
            int index = (int)Math.Ceiling(_p * len) - 1;
            if (index < 0) index = 0;

            // Для медианы (0.5) при четном числе элементов возвращаем среднее двух центральных
            if (_p == 0.5 && len % 2 == 0)
            {
                return (temp[(len / 2) - 1] + temp[len / 2]) / 2.0;
            }

            return temp[index];
        }
    }

    //public class StreamingMedian
    //{
    //    private readonly object _lock = new object();
    //    private long _count = 0;

    //    private readonly double _targetQuantile;
    //    // Высоты 5 маркеров (по сути, оценки минимума, 25%, 50%, 75% и максимума)
    //    private readonly double[] _q = new double[5];
    //    // Текущие фактические позиции маркеров
    //    private readonly double[] _n = new double[5];
    //    // Целевые идеальные позиции маркеров
    //    private readonly double[] _nDesired = new double[5];

    //    public StreamingMedian(double targetQuantile = 0.5)
    //    {
    //        _targetQuantile = targetQuantile;
    //    }

    //    public double Median => _count < 5 ? GetExactMedianBefore5() : _q[2];

    //    public void Update(double value)
    //    {
    //        lock (_lock)
    //        {
    //            _count++;

    //            // Первые 5 элементов просто собираем и сортируем для инициализации
    //            if (_count <= 5)
    //            {
    //                _q[(int)_count - 1] = value;
    //                if (_count == 5)
    //                {
    //                    Array.Sort(_q);
    //                    // Начальные позиции маркеров (1-based индекс: 1, 2, 3, 4, 5)
    //                    for (int i = 0; i < 5; i++) _n[i] = i + 1;

    //                    // Идеальные позиции для медианы (квантиль p = 0.5)
    //                    _nDesired[0] = 1;
    //                    _nDesired[1] = 1 + 2 * 0.5; // 2
    //                    _nDesired[2] = 1 + 4 * 0.5; // 3
    //                    _nDesired[3] = 1 + 6 * 0.5; // 4
    //                    _nDesired[4] = 5;
    //                }
    //                return;
    //            }

    //            // 1. Поиск под интервала, в который попало новое значение, и обновление краев
    //            int k;
    //            if (value < _q[0]) { _q[0] = value; k = 0; }
    //            else if (value < _q[1]) k = 0;
    //            else if (value < _q[2]) k = 1;
    //            else if (value < _q[3]) k = 2;
    //            else if (value <= _q[4]) k = 3;
    //            else { _q[4] = value; k = 3; }

    //            // Сдвигаем фактические позиции всех маркеров справа от вставки
    //            for (int i = k + 1; i < 5; i++) _n[i]++;

    //            // Обновляем идеальные целевые позиции маркеров
    //            _nDesired[1] = 1 + (_count - 1) * 0.25;
    //            _nDesired[2] = 1 + (_count - 1) * 0.50; // Медиана
    //            _nDesired[3] = 1 + (_count - 1) * 0.75;
    //            _nDesired[4] = _count;

    //            // 2. Корректировка позиций внутренних маркеров (1, 2, 3)
    //            for (int i = 1; i <= 3; i++)
    //            {
    //                double d = _nDesired[i] - _n[i];

    //                if ((d >= 1 && _n[i + 1] - _n[i] > 1) || (d <= -1 && _n[i - 1] - _n[i] < -1))
    //                {
    //                    int sign = d > 0 ? 1 : -1;

    //                    // Параболическая формула предсказания высоты маркера
    //                    double qP = _q[i] + (sign / (_n[i + 1] - _n[i - 1])) *
    //                        ((_n[i] - _n[i - 1] + sign) * (_q[i + 1] - _q[i]) / (_n[i + 1] - _n[i]) +
    //                         (_n[i + 1] - _n[i] - sign) * (_q[i] - _q[i - 1]) / (_n[i] - _n[i - 1]));

    //                    // Проверка: высота маркера должна оставаться между соседями
    //                    if (_q[i - 1] < qP && qP < _q[i + 1])
    //                    {
    //                        _q[i] = qP;
    //                    }
    //                    else
    //                    {
    //                        // Если парабола вылетела за рамки, используем линейную интерполяцию
    //                        _q[i] = _q[i] + sign * (_q[i + sign] - _q[i]) / (_n[i + sign] - _n[i]);
    //                    }
    //                    _n[i] += sign;
    //                }
    //            }
    //        }
    //    }

    //    private double GetExactMedianBefore5()
    //    {
    //        if (_count == 0) return 0;
    //        var temp = _q.Take((int)_count).OrderBy(x => x).ToArray();
    //        return temp.Length % 2 != 0 ? temp[temp.Length / 2] : (temp[(temp.Length / 2) - 1] + temp[temp.Length / 2]) / 2.0;
    //    }
    //}
}
