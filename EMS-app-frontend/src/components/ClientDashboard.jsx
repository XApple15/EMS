import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { userService, deviceService } from '../api/api-service';
import './ClientDashboard.css';

const ClientDashboard = () => {
    const navigate = useNavigate();
    const [authUser, setAuthUser] = useState(null);
    const [user, setUser] = useState(null);
    const [devices, setDevices] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [refreshFlag, setRefreshFlag] = useState(0);

    useEffect(() => {
        checkAuthAndLoadData();
    }, [refreshFlag]);

    const checkAuthAndLoadData = async () => {
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

            setAuthUser(storedUser);

            if (storedUser && (storedUser.id || storedUser.Id)) {
                const authId = storedUser.id ?? storedUser.Id;
                await loadUserData(authId);
            } else {
                setLoading(false);
            }
        } catch (err) {
            console.error('Error loading data:', err);
            setError('Failed to load user data');
            setLoading(false);
        }
    };

    const loadUserData = async (authId) => {
        try {
            const userData = await userService.getUserByAuthId(authId);
            setUser(userData);

            if (userData && (userData.id || userData.Id)) {
                const userId = userData.id ?? userData.Id;
                await loadDevices(userId);
            } else {
                setDevices([]);
            }

            setLoading(false);
        } catch (err) {
            console.error('Error fetching user data:', err);
            setError('Failed to load user profile and devices');
            setLoading(false);
        }
    };

    const loadDevices = async (userId) => {
        try {
            const devicesData = await deviceService.getDevicesByUserId(userId);
            const normalized = (devicesData || []).map(d => ({
                ...d,
                consumption: d.consumption ? Number(d.consumption) : 0
            }));
            setDevices(normalized);
        } catch (err) {
            console.error('Error fetching devices:', err);
            setDevices([]);
        }
    };

    const getTotalConsumption = () => {
        const total = devices.reduce((total, device) => total + (Number(device.consumption) || 0), 0);
        return total.toFixed(2);
    };

    const getAverageConsumption = () => {
        if (devices.length === 0) return '0.00';
        const avg = (Number(getTotalConsumption()) / devices.length);
        return avg.toFixed(2);
    };

    const handleLogout = () => {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        localStorage.removeItem('roles');
        navigate('/login');
    };

    if (loading) {
        return (
            <div className="dashboard-container">
                <div className="loading-spinner">
                    <div className="spinner"></div>
                    <p>Loading your dashboard...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="dashboard-container">
            <div className="dashboard-header">
                <div className="header-content">
                    <div>
                        <h1>Client Dashboard</h1>
                        <p className="welcome-text">
                            Welcome back, {authUser?.email || 'User'}!
                        </p>
                    </div>
                    <div className="header-actions">
                        <button onClick={handleLogout} className="logout-button">Logout</button>
                    </div>
                </div>
            </div>

            {error && (
                <div className="error-banner">
                    <p>{error}</p>
                </div>
            )}

            <div className="dashboard-content">
                <div className="stats-grid">
                    <div className="stat-card">
                        <div className="stat-icon devices-icon"></div>
                        <div className="stat-info">
                            <p className="stat-label">Total Devices</p>
                            <p className="stat-value">{devices.length}</p>
                        </div>
                    </div>

                    <div className="stat-card">
                        <div className="stat-icon consumption-icon"></div>
                        <div className="stat-info">
                            <p className="stat-label">Total Consumption</p>
                            <p className="stat-value">{getTotalConsumption()} <span className="unit">kWh</span></p>
                        </div>
                    </div>

                    <div className="stat-card">
                        <div className="stat-icon average-icon"></div>
                        <div className="stat-info">
                            <p className="stat-label">Avg. Consumption</p>
                            <p className="stat-value">{getAverageConsumption()} <span className="unit">kWh</span></p>
                        </div>
                    </div>
                </div>

                <div className="profile-section">
                    <h2>Your Profile</h2>
                    <div className="profile-card">
                        {authUser && (
                            <>
                                <div className="profile-item">
                                    <span className="label">Email:</span>
                                    <span className="value">{authUser.email}</span>
                                </div>
                                <div className="profile-item">
                                    <span className="label">Auth ID:</span>
                                    <span className="value">{authUser.id}</span>
                                </div>
                            </>
                        )}
                        {user && (
                            <>
                                <div className="profile-item">
                                    <span className="label">User ID:</span>
                                    <span className="value">{user.id}</span>
                                </div>
                                {user.username && (
                                    <div className="profile-item">
                                        <span className="label">UserName:</span>
                                        <span className="value">{user.username}</span>
                                    </div>
                                )}
                                {user.address && (
                                    <div className="profile-item">
                                        <span className="label">address:</span>
                                        <span className="value">{user.address}</span>
                                    </div>
                                )}
                                <div className="profile-item">
                                    <span className="label">Role:</span>
                                    <span className="value badge-client">Client</span>
                                </div>
                            </>
                        )}
                    </div>
                </div>

                <div className="devices-section">
                    <h2>Your Devices ({devices.length})</h2>

                    {devices.length === 0 ? (
                        <div className="no-devices">
                            <p>You don't have any devices registered yet.</p>
                        </div>
                    ) : (
                        <div className="devices-grid">
                            {devices.map((device) => (
                                <div key={device.id} className="device-card">
                                    <div className="device-info">
                                        <h3>{device.name || 'Unnamed Device'}</h3>
                                        <div className="device-consumption">
                                            <span className="consumption-label">Consumption:</span>
                                            <span className="consumption-value">{device.consumption || 0} kWh</span>
                                        </div>
                                        <div className="device-meta">
                                            <span className="device-id">ID: {device.id}</span>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default ClientDashboard;