import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
    Chart as ChartJS,
    CategoryScale,
    LinearScale,
    PointElement,
    LineElement,
    BarElement,
    Title,
    Tooltip,
    Legend,
} from 'chart.js';
import { Line, Bar } from 'react-chartjs-2';
import { energyConsumptionService, userService } from '../api/api-service';
import './EnergyConsumptionHistory.css';

// Register Chart.js components
ChartJS.register(
    CategoryScale,
    LinearScale,
    PointElement,
    LineElement,
    BarElement,
    Title,
    Tooltip,
    Legend
);

const EnergyConsumptionHistory = () => {
    const navigate = useNavigate();
    const [selectedDate, setSelectedDate] = useState(getTodayDateString());
    const [chartType, setChartType] = useState('line'); // 'line', 'bar', 'both'
    const [consumptionData, setConsumptionData] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [user, setUser] = useState(null);

    function getTodayDateString() {
        const today = new Date();
        return today.toISOString().split('T')[0];
    }

    const checkAuthAndLoadUser = useCallback(async () => {
        try {
            const token = localStorage.getItem('token');
            const roles = JSON.parse(localStorage.getItem('roles') || '[]');
            const storedUser = JSON.parse(localStorage.getItem('user') || 'null');

            if (!token) {
                navigate('/login');
                return;
            }

            const lowerRoles = (roles || []).map(r => String(r).toLowerCase());
            if (!lowerRoles.includes('client')) {
                navigate('/admin/dashboard');
                return;
            }

            if (storedUser && (storedUser.id || storedUser.Id)) {
                const authId = storedUser.id ?? storedUser.Id;
                try {
                    const userData = await userService.getUserByAuthId(authId);
                    setUser(userData);
                } catch {
                    setError('Failed to load user data');
                }
            }
        } catch {
            setError('Authentication error');
            navigate('/login');
        }
    }, [navigate]);

    useEffect(() => {
        checkAuthAndLoadUser();
    }, [checkAuthAndLoadUser]);

    const fetchConsumptionData = useCallback(async () => {
        if (!user || !(user.id || user.Id)) {
            return;
        }

        const userId = user.id ?? user.Id;
        setLoading(true);
        setError('');

        try {
            const data = await energyConsumptionService.getDailyConsumption(userId, selectedDate);
            setConsumptionData(data);
        } catch (err) {
            console.error('Error fetching consumption data:', err);
            if (err.response?.status === 401) {
                setError('Unauthorized: You can only view your own energy data');
            } else if (err.response?.status === 400) {
                setError('Invalid date format. Please use YYYY-MM-DD format.');
            } else {
                setError('Failed to load energy consumption data. Please try again.');
            }
            setConsumptionData(null);
        } finally {
            setLoading(false);
        }
    }, [user, selectedDate]);

    useEffect(() => {
        if (user) {
            fetchConsumptionData();
        }
    }, [user, selectedDate, fetchConsumptionData]);

    const handleDateChange = (e) => {
        setSelectedDate(e.target.value);
    };

    const handleChartTypeChange = (type) => {
        setChartType(type);
    };

    const handleLogout = () => {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        localStorage.removeItem('roles');
        navigate('/login');
    };

    // Prepare chart data
    const getChartData = () => {
        if (!consumptionData || !consumptionData.data) {
            return null;
        }

        const labels = consumptionData.data.map(d => `${d.hour}:00`);
        const values = consumptionData.data.map(d => d.energyKwh);

        return {
            labels,
            datasets: [
                {
                    label: 'Energy Consumption (kWh)',
                    data: values,
                    borderColor: 'rgb(14, 165, 163)',
                    backgroundColor: 'rgba(14, 165, 163, 0.5)',
                    tension: 0.3,
                    fill: true,
                },
            ],
        };
    };

    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'top',
            },
            title: {
                display: true,
                text: `Energy Consumption for ${selectedDate}`,
                font: {
                    size: 16,
                    weight: 'bold',
                },
            },
            tooltip: {
                callbacks: {
                    label: function(context) {
                        return `${context.parsed.y} kWh`;
                    }
                }
            }
        },
        scales: {
            x: {
                title: {
                    display: true,
                    text: 'Hour of Day',
                    font: {
                        weight: 'bold',
                    },
                },
            },
            y: {
                title: {
                    display: true,
                    text: 'Energy (kWh)',
                    font: {
                        weight: 'bold',
                    },
                },
                beginAtZero: true,
            },
        },
    };

    const chartData = getChartData();

    // Calculate statistics
    const getStatistics = () => {
        if (!consumptionData || !consumptionData.data) {
            return { total: 0, average: 0, peak: 0, peakHour: 0 };
        }

        const values = consumptionData.data.map(d => d.energyKwh);
        const total = values.reduce((sum, val) => sum + val, 0);
        const average = total / values.length;
        const peak = Math.max(...values);
        const peakHour = consumptionData.data.find(d => d.energyKwh === peak)?.hour ?? 0;

        return {
            total: total.toFixed(2),
            average: average.toFixed(2),
            peak: peak.toFixed(2),
            peakHour,
        };
    };

    const stats = getStatistics();

    return (
        <div className="energy-history-container">
            <div className="energy-history-header">
                <div className="header-content">
                    <div>
                        <h1>Energy Consumption History</h1>
                        <p className="subtitle">View your hourly energy usage patterns</p>
                    </div>
                    <div className="header-actions">
                        <button onClick={() => navigate('/client/dashboard')} className="back-button">
                            Back to Dashboard
                        </button>
                        <button onClick={handleLogout} className="logout-button">
                            Logout
                        </button>
                    </div>
                </div>
            </div>

            {error && (
                <div className="error-banner">
                    <p>{error}</p>
                </div>
            )}

            <div className="controls-section">
                <div className="date-picker-container">
                    <label htmlFor="date-picker">Select Date:</label>
                    <input
                        type="date"
                        id="date-picker"
                        value={selectedDate}
                        onChange={handleDateChange}
                        max={getTodayDateString()}
                        className="date-input"
                    />
                </div>

                <div className="chart-type-container">
                    <span className="chart-type-label">Chart Type:</span>
                    <div className="chart-type-buttons">
                        <button
                            className={`chart-type-btn ${chartType === 'line' ? 'active' : ''}`}
                            onClick={() => handleChartTypeChange('line')}
                        >
                            Line
                        </button>
                        <button
                            className={`chart-type-btn ${chartType === 'bar' ? 'active' : ''}`}
                            onClick={() => handleChartTypeChange('bar')}
                        >
                            Bar
                        </button>
                        <button
                            className={`chart-type-btn ${chartType === 'both' ? 'active' : ''}`}
                            onClick={() => handleChartTypeChange('both')}
                        >
                            Both
                        </button>
                    </div>
                </div>
            </div>

            {loading ? (
                <div className="loading-container">
                    <div className="spinner"></div>
                    <p>Loading energy consumption data...</p>
                </div>
            ) : consumptionData && chartData ? (
                <>
                    <div className="stats-grid">
                        <div className="stat-card">
                            <div className="stat-info">
                                <p className="stat-label">Total Daily Consumption</p>
                                <p className="stat-value">{stats.total} <span className="unit">kWh</span></p>
                            </div>
                        </div>
                        <div className="stat-card">
                            <div className="stat-info">
                                <p className="stat-label">Average Hourly</p>
                                <p className="stat-value">{stats.average} <span className="unit">kWh</span></p>
                            </div>
                        </div>
                        <div className="stat-card">
                            <div className="stat-info">
                                <p className="stat-label">Peak Consumption</p>
                                <p className="stat-value">{stats.peak} <span className="unit">kWh</span></p>
                            </div>
                        </div>
                        <div className="stat-card">
                            <div className="stat-info">
                                <p className="stat-label">Peak Hour</p>
                                <p className="stat-value">{stats.peakHour}:00</p>
                            </div>
                        </div>
                    </div>

                    <div className="charts-section">
                        {(chartType === 'line' || chartType === 'both') && (
                            <div className="chart-container">
                                <h3>Line Chart</h3>
                                <div className="chart-wrapper">
                                    <Line data={chartData} options={chartOptions} />
                                </div>
                            </div>
                        )}

                        {(chartType === 'bar' || chartType === 'both') && (
                            <div className="chart-container">
                                <h3>Bar Chart</h3>
                                <div className="chart-wrapper">
                                    <Bar data={chartData} options={chartOptions} />
                                </div>
                            </div>
                        )}
                    </div>

                    <div className="data-table-section">
                        <h3>Hourly Breakdown</h3>
                        <div className="data-table-container">
                            <table className="data-table">
                                <thead>
                                    <tr>
                                        <th>Hour</th>
                                        <th>Energy (kWh)</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {consumptionData.data.map((item) => (
                                        <tr key={item.hour} className={item.energyKwh === parseFloat(stats.peak) ? 'peak-row' : ''}>
                                            <td>{item.hour}:00</td>
                                            <td>{item.energyKwh}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </>
            ) : !loading && !error ? (
                <div className="no-data-container">
                    <p>No energy consumption data available for the selected date.</p>
                    <p>Please select a different date to view your energy usage history.</p>
                </div>
            ) : null}
        </div>
    );
};

export default EnergyConsumptionHistory;
