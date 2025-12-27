import React, { useEffect, useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { userService, deviceService } from '../api/api-service';
import './AdminDashboard.css';
import UserFormModal from './UserFormModal';

const DeviceFormModal = ({ initial, onClose, onSaved, users }) => {
    const [form, setForm] = useState({
        name: initial?.name ?? '',
        consumption: initial?.consumption ?? '',
        userId: initial?.userId ?? null,
    });
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        setForm({
            name: initial?.name ?? '',
            consumption: initial?.consumption ?? '',
            userId: initial?.userId ?? null,
        });
    }, [initial]);

    const handleChange = (e) => {
        const { name, value } = e.target;
        setForm((prev) => ({ ...prev, [name]: name === 'userId' ? (value || null) : value }));
        setError('');
    };

    const validate = () => {
        if (!form.name || form.name.trim().length === 0) {
            setError('Device name is required');
            return false;
        }
        if (form.consumption === '' || isNaN(Number(form.consumption))) {
            setError('Consumption must be a valid number');
            return false;
        }
        return true;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (!validate()) return;
        setSaving(true);
        try {
            const dto = {
                userId: form.userId || null,
                name: form.name.trim(),
                consumption: String(form.consumption).trim(),
            };

            if (initial?.id) {
                await deviceService.updateDevice(initial.id, dto);
            } else {
                await deviceService.createDevice(dto);
            }

            onSaved && onSaved();
            onClose && onClose();
        } catch (err) {
            console.error('Save device failed', err);
            setError(err.response?.data?.message || err.message || 'Failed to save device');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal-content" onClick={(e) => e.stopPropagation()}>
                <div className="modal-header">
                    <h3>{initial?.id ? 'Edit Device' : 'Create Device'}</h3>
                    <button className="close-button" onClick={onClose}>&times;</button>
                </div>

                <form className="device-form" onSubmit={handleSubmit}>
                    {error && <div className="form-error">{error}</div>}

                    <div className="form-row">
                        <label>Name</label>
                        <input
                            name="name"
                            value={form.name}
                            onChange={handleChange}
                            placeholder="Device name"
                            disabled={saving}
                        />
                    </div>

                    <div className="form-row">
                        <label>Consumption (kWh)</label>
                        <input
                            name="consumption"
                            value={form.consumption}
                            onChange={handleChange}
                            placeholder="e.g. 12.5"
                            disabled={saving}
                        />
                    </div>

                    <div className="form-row">
                        <label>Assign to User</label>
                        <select
                            name="userId"
                            value={form.userId || ''}
                            onChange={handleChange}
                            disabled={saving}
                        >
                            <option value="">Unassigned</option>
                            {users.map((u) => (
                                <option key={u.id} value={u.id}>
                                    {u.username || u.userName || u.email || u.id}
                                </option>
                            ))}
                        </select>
                    </div>

                    <div className="form-actions">
                        <button type="submit" disabled={saving}>
                            {saving ? 'Saving...' : 'Save'}
                        </button>
                        <button type="button" className="btn-secondary" onClick={onClose} disabled={saving}>
                            Cancel
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const AdminDashboard = () => {
    const navigate = useNavigate();
    const [users, setUsers] = useState([]);
    const [devices, setDevices] = useState([]);
    const [loading, setLoading] = useState(true);
    const [devicesLoading, setDevicesLoading] = useState(true);
    const [error, setError] = useState('');
    const [showForm, setShowForm] = useState(false);
    const [editingUser, setEditingUser] = useState(null);

    const [showDeviceForm, setShowDeviceForm] = useState(false);
    const [editingDevice, setEditingDevice] = useState(null);

    useEffect(() => {
        checkAuthAndLoad();
    }, []);

    const checkAuthAndLoad = async () => {
        try {
            const token = localStorage.getItem('token');
            const roles = JSON.parse(localStorage.getItem('roles') || '[]');
            const lowerRoles = (roles || []).map(r => String(r).toLowerCase());

            if (!token) {
                navigate('/login');
                return;
            }
            if (!lowerRoles.includes('admin')) {
                navigate('/client/dashboard');
                return;
            }
            await Promise.all([loadUsers(), loadDevices()]);
        } catch (err) {
            console.error(err);
            setError('Failed to initialize admin dashboard');
            setLoading(false);
            setDevicesLoading(false);
        }
    };

    const loadUsers = async () => {
        setLoading(true);
        setError('');
        try {
            const token = localStorage.getItem('token');
            console.log('Token exists:', !!token);
            console.log('Token preview:', token?.substring(0, 50) + '...');

            const data = await userService.getAllUsers();
            setUsers(Array.isArray(data) ? data : []);
        } catch (err) {
            console.error('Failed to load users', err);
            setError(err.response?.data?.message || 'Failed to load users');
            setUsers([]);
        } finally {
            setLoading(false);
        }
    };

    const loadDevices = async () => {
        setDevicesLoading(true);
        try {
            const all = await deviceService.getAllDevices();
            setDevices(Array.isArray(all) ? all : []);
        } catch (err) {
            console.error('Failed to load devices', err);
            setDevices([]);
        } finally {
            setDevicesLoading(false);
        }
    };

    const handleAdd = () => {
        setEditingUser(null);
        setShowForm(true);
    };

    const handleEdit = (user) => {
        setEditingUser(user);
        setShowForm(true);
    };

    const handleDelete = async (user) => {
        if (!window.confirm(`Delete user ${user.username || user.email}? This will remove both auth and profile.`)) return;
        try {
            await userService.deleteUser(user.id);
            await loadUsers();
        } catch (err) {
            console.error('Delete user failed', err);
            alert(err.response?.data?.message || 'Failed to delete user');
        }
    };

    const handleUserSaved = async () => {
        setShowForm(false);
        setEditingUser(null);
        await loadUsers();
    };

    const handleAddDevice = () => {
        setEditingDevice(null);
        setShowDeviceForm(true);
    };

    const handleEditDevice = (device) => {
        setEditingDevice(device);
        setShowDeviceForm(true);
    };

    const handleDeleteDevice = async (device) => {
        if (!window.confirm(`Delete device "${device.name}"?`)) return;
        try {
            await deviceService.deleteDevice(device.id);
            await loadDevices();
        } catch (err) {
            console.error('Delete device failed', err);
            alert(err.response?.data?.message || 'Failed to delete device');
        }
    };

    const handleDeviceSaved = async () => {
        setShowDeviceForm(false);
        setEditingDevice(null);
        await loadDevices();
    };

    const userLabelById = useMemo(() => {
        const map = new Map();
        users.forEach(u => map.set(u.id, u.username || u.userName || u.email || u.id));
        return (id) => map.get(id) || '';
    }, [users]);

    return (
        <div className="admin-dashboard">
            <div className="admin-header">
                <h1>Admin Dashboard</h1>
                <div className="admin-actions">
                    <button className="primary" onClick={handleAdd}>Create User</button>
                    <button className="primary" onClick={handleAddDevice}>Create Device</button>
                    <button 
                        className="primary" 
                        onClick={() => navigate('/admin/chat')}
                        style={{
                            background: 'linear-gradient(to right, #3b82f6, #2563eb)',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '8px'
                        }}
                    >
                        <span>💬</span>
                        <span>Admin Chat</span>
                    </button>
                </div>
            </div>

            {error && <div className="error-banner">{error}</div>}

           
            {loading ? (
                <div className="loading">Loading users...</div>
            ) : (
                <div className="users-table-wrap">
                    <h2>Users</h2>
                    <table className="users-table">
                        <thead>
                            <tr>
                                <th>Auth ID</th>
                                <th>User ID</th>
                                <th>Username</th>
                                <th>Email</th>
                                <th>Address</th>
                                <th>Roles</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {users.map(u => (
                                <tr key={u.id}>
                                    <td>{u.authId || u.authID || u.authIdString || ''}</td>
                                    <td>{u.id}</td>
                                    <td>{u.username || u.userName || ''}</td>
                                    <td>{u.email || ''}</td>
                                    <td>{u.address || ''}</td>
                                    <td>{(u.roles && u.roles.join ? u.roles.join(', ') : (u.role || ''))}</td>
                                    <td>
                                        <button onClick={() => handleEdit(u)}>Edit</button>
                                        <button className="danger" onClick={() => handleDelete(u)}>Delete</button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {users.length === 0 && <p className="empty">No users found.</p>}
                </div>
            )}

            
            {devicesLoading ? (
                <div className="loading">Loading devices...</div>
            ) : (
                <div className="devices-table-wrap" style={{ marginTop: 20 }}>
                    <h2>Devices</h2>
                    <table className="users-table">
                        <thead>
                            <tr>
                                <th>Device ID</th>
                                <th>Name</th>
                                <th>Consumption (kWh)</th>
                                <th>Assigned To (User)</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {devices.map(d => (
                                <tr key={d.id}>
                                    <td>{d.id}</td>
                                    <td>{d.name}</td>
                                    <td>{d.consumption}</td>
                                    <td>{d.userId ? userLabelById(d.userId) : 'Unassigned'}</td>
                                    <td>
                                        <button onClick={() => handleEditDevice(d)}>Edit / Assign</button>
                                        <button className="danger" onClick={() => handleDeleteDevice(d)}>Delete</button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {devices.length === 0 && <p className="empty">No devices found.</p>}
                </div>
            )}

            
            {showForm && (
                <UserFormModal
                    initial={editingUser}
                    onClose={() => { setShowForm(false); setEditingUser(null); }}
                    onSaved={handleUserSaved}
                />
            )}

            {showDeviceForm && (
                <DeviceFormModal
                    initial={editingDevice}
                    onClose={() => { setShowDeviceForm(false); setEditingDevice(null); }}
                    onSaved={handleDeviceSaved}
                    users={users}
                />
            )}
        </div>
    );
};

export default AdminDashboard;