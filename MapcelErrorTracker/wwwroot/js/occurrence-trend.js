(function () {
    const rangeButtonSelector = '[data-range]';
    const bucketLabels = {
        auto: 'Auto',
        minute: 'minuto',
        hour: 'hora',
        day: 'día',
        week: 'semana'
    };

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-occurrence-trend]').forEach(initializeOccurrenceTrend);
    });

    function initializeOccurrenceTrend(root) {
        const state = {
            root,
            maxBuckets: Number.parseInt(root.dataset.maxBuckets || '500', 10),
            selectedRange: 'historical',
            lastPresetRange: 'historical',
            summary: null,
            chart: null,
            abortController: null,
            debounceTimer: 0
        };

        bindControls(state);
        setActiveRange(state, state.selectedRange);
        loadSummary(state);
    }

    function bindControls(state) {
        state.root.querySelectorAll(rangeButtonSelector).forEach(function (button) {
            button.addEventListener('click', function () {
                const previousRange = state.selectedRange;
                state.selectedRange = button.dataset.range;

                if (state.selectedRange !== 'custom') {
                    state.lastPresetRange = state.selectedRange;
                }

                setActiveRange(state, state.selectedRange);
                toggleCustomRange(state);

                if (state.selectedRange === 'custom') {
                    seedCustomInputs(state, previousRange, true);
                }

                reloadChart(state).then(() => {});
            });
        });

        state.root.querySelector('[data-bucket-select]')?.addEventListener('change', function () {
            reloadChart(state);
        });

        state.root.querySelectorAll('[data-custom-from], [data-custom-to]').forEach(function (input) {
            input.addEventListener('change', function () {
                scheduleReload(state);
            });
        });
    }

    async function loadSummary(state) {
        showState(state, 'Cargando tendencia...', true);

        try {
            const response = await fetch(state.root.dataset.summaryUrl, {
                headers: { Accept: 'application/json' }
            });

            if (response.status === 404) {
                showState(state, 'No hay datos de ocurrencias disponibles.', true);
                updateSummaryStats(state, null);
                return;
            }

            if (!response.ok) {
                throw new Error('Summary request failed.');
            }

            state.summary = await response.json();
            updateSummaryStats(state, state.summary);
            seedCustomInputs(state, state.lastPresetRange, false);
            reloadChart(state);
        } catch (error) {
            showState(state, 'No se pudo cargar la tendencia de ocurrencias.', true);
            updateSummaryStats(state, null);
        }
    }

    function scheduleReload(state) {
        window.clearTimeout(state.debounceTimer);
        state.debounceTimer = window.setTimeout(function () {
            reloadChart(state);
        }, 250);
    }

    async function reloadChart(state) {
        const selectedBucket = state.root.querySelector('[data-bucket-select]')?.value || 'auto';
        const range = getSelectedRange(state);
        const validationMessage = validateRequest(state, range, selectedBucket);

        if (validationMessage) {
            destroyChart(state);
            showState(state, validationMessage, true);
            updateRangeStats(state, null);
            return;
        }

        if (state.abortController) {
            state.abortController.abort();
        }

        state.abortController = new AbortController();
        showState(state, 'Cargando tendencia...', true);

        const url = new URL(state.root.dataset.histogramUrl, window.location.origin);
        url.searchParams.set('from', formatDateTimeQuery(range.from));
        url.searchParams.set('to', formatDateTimeQuery(range.to));
        url.searchParams.set('bucket', selectedBucket);

        try {
            const response = await fetch(url, {
                headers: { Accept: 'application/json' },
                signal: state.abortController.signal
            });

            if (response.status === 404) {
                destroyChart(state);
                showState(state, 'No hay datos de ocurrencias disponibles.', true);
                updateRangeStats(state, null);
                return;
            }

            if (!response.ok) {
                const message = response.status === 400
                    ? 'El rango seleccionado genera demasiados puntos.'
                    : 'No se pudo cargar la tendencia de ocurrencias.';
                destroyChart(state);
                showState(state, message, true);
                updateRangeStats(state, null);
                return;
            }

            const histogram = await response.json();
            renderChart(state, histogram);
            updateRangeStats(state, histogram);
            updateBucketLabel(state, selectedBucket, histogram.bucket);

            if (!histogram.buckets || histogram.buckets.length === 0 || histogram.buckets.every(function (bucket) { return getNumber(bucket, 'occurrences') === 0; })) {
                showState(state, 'No hay ocurrencias en este rango.', true);
                return;
            }

            showState(state, '', false);
        } catch (error) {
            if (error.name === 'AbortError') {
                return;
            }

            destroyChart(state);
            showState(state, 'No se pudo cargar la tendencia de ocurrencias.', true);
            updateRangeStats(state, null);
        }
    }

    function renderChart(state, histogram) {
        if (!window.Chart) {
            showState(state, 'No se pudo cargar Chart.js.', true);
            return;
        }

        const canvas = state.root.querySelector('[data-occurrence-chart]');
        const buckets = histogram.buckets || [];
        const labels = buckets.map(function (bucket) {
            return formatBucketLabel(parseDate(getValue(bucket, 'from')), histogram.bucket);
        });
        const values = buckets.map(function (bucket) {
            return getNumber(bucket, 'occurrences');
        });

        const data = {
            labels,
            datasets: [
                {
                    label: 'Ocurrencias',
                    data: values,
                    borderColor: '#0f766e',
                    backgroundColor: 'rgba(20, 184, 166, 0.12)',
                    pointBackgroundColor: '#0f766e',
                    pointBorderColor: '#ffffff',
                    pointRadius: values.length > 120 ? 0 : 2.5,
                    pointHoverRadius: 4,
                    borderWidth: 2,
                    fill: true,
                    tension: 0.25
                }
            ]
        };

        const options = {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                intersect: false,
                mode: 'index'
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        title: function (items) {
                            const bucket = buckets[items[0].dataIndex];
                            return formatBucketRange(bucket);
                        },
                        label: function (item) {
                            return item.formattedValue + ' ocurrencias';
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: {
                        display: false
                    },
                    ticks: {
                        autoSkip: true,
                        maxRotation: 0,
                        maxTicksLimit: 8
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        color: '#e5e7eb'
                    },
                    ticks: {
                        precision: 0
                    }
                }
            }
        };

        if (state.chart) {
            state.chart.data = data;
            state.chart.options = options;
            state.chart.update();
            return;
        }

        state.chart = new Chart(canvas, {
            type: 'line',
            data,
            options
        });
    }

    function getSelectedRange(state) {
        const now = new Date();
        const summaryFirst = parseDate(getValue(state.summary, 'firstOccurrenceAt')) || parseDate(state.root.dataset.initialFirstSeen);
        const summaryLast = parseDate(getValue(state.summary, 'lastOccurrenceAt')) || parseDate(state.root.dataset.initialLastSeen) || now;

        if (state.selectedRange === '24h') {
            return { from: addHours(now, -24), to: now };
        }

        if (state.selectedRange === '7d') {
            return { from: addDays(now, -7), to: now };
        }

        if (state.selectedRange === '30d') {
            return { from: addDays(now, -30), to: now };
        }

        if (state.selectedRange === 'custom') {
            return {
                from: parseDateTimeInput(state.root.querySelector('[data-custom-from]')?.value),
                to: parseDateTimeInput(state.root.querySelector('[data-custom-to]')?.value)
            };
        }

        const from = summaryFirst || addHours(summaryLast, -1);
        let to = addSeconds(summaryLast, 1);

        if (from >= to) {
            to = addHours(from, 1);
        }

        return { from, to };
    }

    function validateRequest(state, range, selectedBucket) {
        if (!range.from || !range.to) {
            return 'Selecciona inicio y fin para el rango personalizado.';
        }

        if (range.from >= range.to) {
            return 'La fecha final debe ser posterior a la fecha inicial.';
        }

        const resolvedBucket = selectedBucket === 'auto'
            ? resolveAutoBucket(range.from, range.to)
            : selectedBucket;
        const bucketCount = getBucketCount(range.from, range.to, resolvedBucket);

        if (!Number.isFinite(bucketCount) || bucketCount <= 0) {
            return 'El rango seleccionado no es válido.';
        }

        if (bucketCount > state.maxBuckets) {
            const label = bucketLabels[resolvedBucket] || resolvedBucket;
            return 'El rango genera ' + bucketCount.toLocaleString('es-MX') + ' buckets por ' + label + '. Reduce el rango o usa un bucket mayor.';
        }

        return '';
    }

    function setActiveRange(state, range) {
        state.root.querySelectorAll(rangeButtonSelector).forEach(function (button) {
            const isActive = button.dataset.range === range;
            button.classList.toggle('border-teal-500', isActive);
            button.classList.toggle('bg-teal-50', isActive);
            button.classList.toggle('text-teal-700', isActive);
            button.classList.toggle('border-gray-200', !isActive);
            button.classList.toggle('text-gray-500', !isActive);
        });
    }

    function toggleCustomRange(state) {
        const customRange = state.root.querySelector('[data-custom-range]');
        if (!customRange) {
            return;
        }

        const visible = state.selectedRange === 'custom';
        customRange.classList.toggle('hidden', !visible);
        customRange.classList.toggle('grid', visible);
    }

    function seedCustomInputs(state, rangeName, overwrite) {
        const currentRange = getSelectedRange({ ...state, selectedRange: rangeName || state.lastPresetRange });
        const fromInput = state.root.querySelector('[data-custom-from]');
        const toInput = state.root.querySelector('[data-custom-to]');

        if (fromInput && (overwrite || !fromInput.value)) {
            fromInput.value = formatDateTimeInput(currentRange.from);
        }

        if (toInput && (overwrite || !toInput.value)) {
            toInput.value = formatDateTimeInput(currentRange.to);
        }
    }

    function updateSummaryStats(state, summary) {
        setText(state.root, '[data-stat-first-seen]', summary ? formatDateTime(parseDate(getValue(summary, 'firstOccurrenceAt'))) : '...');
        setText(state.root, '[data-stat-last-seen]', summary ? formatDateTime(parseDate(getValue(summary, 'lastOccurrenceAt'))) : '...');
        setText(state.root, '[data-stat-total]', summary ? formatNumber(getNumber(summary, 'totalOccurrences')) : '...');
        setText(state.root, '[data-stat-heat]', summary ? formatNumber(getNumber(summary, 'heatScore')) + ' · ' + (getValue(summary, 'calculatedPriority') || '') : '...');
    }

    function updateRangeStats(state, histogram) {
        if (!histogram || !histogram.buckets || histogram.buckets.length === 0) {
            setText(state.root, '[data-stat-peak]', '...');
            setText(state.root, '[data-stat-current]', '...');
            updateBucketLabel(state, state.root.querySelector('[data-bucket-select]')?.value || 'auto', '');
            return;
        }

        const buckets = histogram.buckets;
        const peak = buckets.reduce(function (winner, bucket) {
            return getNumber(bucket, 'occurrences') > getNumber(winner, 'occurrences') ? bucket : winner;
        }, buckets[0]);
        const current = buckets[buckets.length - 1];

        setText(state.root, '[data-stat-peak]', formatNumber(getNumber(peak, 'occurrences')) + ' · ' + formatBucketRange(peak));
        setText(state.root, '[data-stat-current]', formatNumber(getNumber(current, 'occurrences')) + ' · ' + formatBucketRange(current));
    }

    function updateBucketLabel(state, selectedBucket, resolvedBucket) {
        const label = state.root.querySelector('[data-occurrence-trend-bucket-label]');
        if (!label) {
            return;
        }

        if (!resolvedBucket) {
            label.textContent = '';
            return;
        }

        label.textContent = selectedBucket === 'auto'
            ? '(Auto: ' + (bucketLabels[resolvedBucket] || resolvedBucket) + ')'
            : '(' + (bucketLabels[resolvedBucket] || resolvedBucket) + ')';
    }

    function showState(state, message, visible) {
        const element = state.root.querySelector('[data-chart-state]');
        if (!element) {
            return;
        }

        element.textContent = message;
        element.classList.toggle('hidden', !visible);
        element.classList.toggle('flex', visible);
    }

    function destroyChart(state) {
        if (!state.chart) {
            return;
        }

        state.chart.destroy();
        state.chart = null;
    }

    function getBucketCount(from, to, bucket) {
        const ms = to - from;
        const minute = 60 * 1000;
        const hour = 60 * minute;
        const day = 24 * hour;
        const week = 7 * day;

        if (bucket === 'minute') {
            return Math.ceil(ms / minute);
        }

        if (bucket === 'hour') {
            return Math.ceil(ms / hour);
        }

        if (bucket === 'day') {
            return Math.ceil(ms / day);
        }

        return Math.ceil(ms / week);
    }

    function resolveAutoBucket(from, to) {
        const hours = (to - from) / (60 * 60 * 1000);
        const days = hours / 24;

        if (hours <= 6) {
            return 'minute';
        }

        if (hours <= 48) {
            return 'hour';
        }

        return days <= 90 ? 'day' : 'week';
    }

    function getValue(source, key) {
        if (!source) {
            return null;
        }

        const pascal = key.charAt(0).toUpperCase() + key.slice(1);
        return source[key] ?? source[pascal] ?? null;
    }

    function getNumber(source, key) {
        const value = getValue(source, key);
        const number = Number(value);
        return Number.isFinite(number) ? number : 0;
    }

    function parseDate(value) {
        if (!value) {
            return null;
        }

        const parsed = new Date(value);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    function parseDateTimeInput(value) {
        return value ? parseDate(value) : null;
    }

    function formatDateTimeQuery(value) {
        return value.getFullYear() + '-' +
            pad(value.getMonth() + 1) + '-' +
            pad(value.getDate()) + 'T' +
            pad(value.getHours()) + ':' +
            pad(value.getMinutes()) + ':' +
            pad(value.getSeconds());
    }

    function formatDateTimeInput(value) {
        return value.getFullYear() + '-' +
            pad(value.getMonth() + 1) + '-' +
            pad(value.getDate()) + 'T' +
            pad(value.getHours()) + ':' +
            pad(value.getMinutes());
    }

    function formatDateTime(value) {
        if (!value) {
            return '...';
        }

        return new Intl.DateTimeFormat('es-MX', {
            dateStyle: 'medium',
            timeStyle: 'short'
        }).format(value);
    }

    function formatBucketLabel(value, bucket) {
        if (!value) {
            return '';
        }

        if (bucket === 'minute' || bucket === 'hour') {
            return new Intl.DateTimeFormat('es-MX', {
                day: '2-digit',
                month: 'short',
                hour: '2-digit',
                minute: '2-digit'
            }).format(value);
        }

        return new Intl.DateTimeFormat('es-MX', {
            day: '2-digit',
            month: 'short'
        }).format(value);
    }

    function formatBucketRange(bucket) {
        const from = parseDate(getValue(bucket, 'from'));
        const to = parseDate(getValue(bucket, 'to'));

        return formatDateTime(from) + ' - ' + formatDateTime(to);
    }

    function formatNumber(value) {
        return Number(value || 0).toLocaleString('es-MX');
    }

    function setText(root, selector, value) {
        const element = root.querySelector(selector);
        if (element) {
            element.textContent = value;
        }
    }

    function addSeconds(value, count) {
        return new Date(value.getTime() + count * 1000);
    }

    function addHours(value, count) {
        return new Date(value.getTime() + count * 60 * 60 * 1000);
    }

    function addDays(value, count) {
        return new Date(value.getTime() + count * 24 * 60 * 60 * 1000);
    }

    function pad(value) {
        return String(value).padStart(2, '0');
    }
})();
