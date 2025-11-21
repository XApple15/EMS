import React, { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { userService } from '../api/api-service';



const UserFormModal = ({ initial, onClose, onSaved }) => {
    const [form, setForm] = useState({
        name: '',
        email: '',
        password: '',
        confirmPassword: '',
        address: '',
        roles: ['Client'],
    });
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        if (initial) {
            setForm({
                name: initial.username ?? initial.userName ?? '',
                email: initial.email ?? '',
                password: '',
                confirmPassword: '',
                address: initial.address ?? '',
                roles: initial.roles ?? ['Client'],
            });
        } else {
            setForm({
                name: '',
                email: '',
                password: '',
                confirmPassword: '',
                address: '',
                roles: ['Client'],
            });
        }
    }, [initial]);

    const handleChange = (e) => {
        const { name, value, type, checked } = e.target;
        if (name === 'roles') {
            setForm(prev => ({ ...prev, roles: checked ? [value] : [] }));
        } else {
            setForm(prev => ({ ...prev, [name]: value }));
        }
        setError('');
    };

    const validate = () => {
        if (!form.name || form.name.trim().length < 2) {
            setError('Name is required (min 2 chars).');
            return false;
        }
        /*if (!form.email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
            setError('A valid email is required.');
            return false;
        }*/
        if (!initial && (!form.password || form.password.length < 6)) {
            setError('Password is required and must be at least 6 characters.');
            return false;
        }
        if (!initial && form.password !== form.confirmPassword) {
            setError('Passwords do not match.');
            return false;
        }
        return true;
    };

    const handleSubmit = async (ev) => {
        ev.preventDefault();
        setError('');
        if (!validate()) return;
        setSaving(true);

        try {
            if (!initial) {
                const createPayload = {
                    name: form.name.trim(),
                    email: form.email.trim(),
                    password: form.password,
                    roles: form.roles && form.roles.length ? form.roles : ['Client'],
                    address: form.address || ''
                };
                await userService.createUser(createPayload);
            } else {
               
                const dto = {
                    AuthId: initial.authId ?? initial.authID ?? initial.authIdString ?? initial.authId,
                    Username: form.name.trim(),
                    Address: form.address || ''
                };
                await userService.updateUser(initial.id, dto);
            }

            onSaved && onSaved();
            onClose && onClose();
        } catch (err) {
            console.error('Save user failed', err);
            setError(err.response?.data?.message || err.message || 'Failed to save user');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal-content" onClick={(e) => e.stopPropagation()}>
                <div className="modal-header">
                    <h3>{initial ? 'Edit User' : 'Create User'}</h3>
                    <button className="close-button" onClick={onClose}>&times;</button>
                </div>

                <form className="device-form" onSubmit={handleSubmit}>
                    {error && <div className="form-error">{error}</div>}

                    <div className="form-row">
                        <label>Full name</label>
                        <input name="name" value={form.name} onChange={handleChange} disabled={saving} />
                    </div>

                    <div className="form-row">
                        <label>Email</label>
                        <input name="email" value={form.email} onChange={handleChange} disabled={saving} />
                    </div>

                    {!initial && (
                        <>
                            <div className="form-row">
                                <label>Password</label>
                                <input type="password" name="password" value={form.password} onChange={handleChange} disabled={saving} />
                            </div>
                            <div className="form-row">
                                <label>Confirm password</label>
                                <input type="password" name="confirmPassword" value={form.confirmPassword} onChange={handleChange} disabled={saving} />
                            </div>
                        </>
                    )}

                    <div className="form-row">
                        <label>Address</label>
                        <input name="address" value={form.address} onChange={handleChange} disabled={saving} />
                    </div>

                    <div className="form-row">
                        <label>Role</label>
                        <div>
                            <label><input type="checkbox" name="roles" value="Admin" checked={form.roles.includes('Admin')} onChange={handleChange} /> Admin</label>
                            <label style={{ marginLeft: 12 }}><input type="checkbox" name="roles" value="Client" checked={form.roles.includes('Client')} onChange={handleChange} /> Client</label>
                        </div>
                    </div>

                    <div className="form-actions">
                        <button type="submit" disabled={saving}>{saving ? 'Saving...' : 'Save'}</button>
                        <button type="button" className="btn-secondary" onClick={onClose} disabled={saving}>Cancel</button>
                    </div>
                </form>
            </div>
        </div>
    );
};

UserFormModal.propTypes = {
    initial: PropTypes.object,
    onClose: PropTypes.func,
    onSaved: PropTypes.func,
};

export default UserFormModal;   