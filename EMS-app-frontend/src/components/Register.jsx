import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import axiosInstance, { userClient } from '../api/axios';
import './Register.css';

const Register = () => {
    const navigate = useNavigate();
    const [formData, setFormData] = useState({
        name: '',
        email: '',
        password: '',
        confirmPassword: '',
        address: '',
    });
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    const handleChange = (e) => {
        setFormData((prev) => ({
            ...prev,
            [e.target.name]: e.target.value,
        }));
        setError('');
    };

    const validateForm = () => {
        if (!formData.name || !formData.email || !formData.password || !formData.confirmPassword) {
            setError('Please fill in all fields');
            return false;
        }

        if (formData.name.length < 2) {
            setError('Name must be at least 2 characters long');
            return false;
        }

        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(formData.email)) {
            setError('Please enter a valid email address');
            return false;
        }

        if (formData.password.length < 6) {
            setError('Password must be at least 6 characters long');
            return false;
        }

        if (formData.password !== formData.confirmPassword) {
            setError('Passwords do not match');
            return false;
        }

        return true;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');

        if (!validateForm()) {
            return;
        }

        setLoading(true);

        const authRequest = {
            email: formData.email,
            password: formData.password,
            address : formData.address || '',
            roles: ['Client'],
        };

        let createdAuthId = null;

        try {
            const authResp = await axiosInstance.post('/auth/register', authRequest);
            const authData = authResp.data;

            createdAuthId =
                authData?.id ??
                authData?.user?.id ??
                authData?.authId ??
                authData?.userId ??
                null;

            if (!createdAuthId) {
                console.warn('Could not determine created auth id from response:', authData);
                throw new Error('Registration succeeded but no auth id was returned by auth service.');
            }
         

            const jwt = authResp.data?.jwtToken ?? authResp.data?.token ?? null;
            if (jwt) {
                localStorage.setItem('token', jwt);
            }
            if (authResp.data?.user) {
                localStorage.setItem('user', JSON.stringify(authResp.data.user));
            }
            if (authResp.data?.roles) {
                localStorage.setItem('roles', JSON.stringify(authResp.data.roles));
            }

            navigate('/login');
        } catch (err) {
            console.error('Registration error:', err);

            if (createdAuthId) {
                try {
                    await axiosInstance.delete(`/auth/${createdAuthId}`);
                    console.log('Rolled back auth user with id', createdAuthId);
                } catch (cleanupErr) {
                    console.error('Failed to rollback auth user:', cleanupErr);
                }
            }

            if (err.response) {
                setError(err.response.data?.message || 'Registration failed. Please try again.');
            } else if (err.request) {
                setError('No response from server. Please check your connection.');
            } else {
                setError(err.message || 'An error occurred. Please try again.');
            }
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="register-container">
            <div className="register-card">
                <h2 className="register-title">Create Account</h2>
                <p className="register-subtitle">Sign up to get started</p>

                {error && <div className="error-message">{error}</div>}

                <form onSubmit={handleSubmit} className="register-form">
                    <div className="form-group">
                        <label htmlFor="name">Full Name</label>
                        <input
                            type="text"
                            id="name"
                            name="name"
                            value={formData.name}
                            onChange={handleChange}
                            placeholder="Enter your full name"
                            disabled={loading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="email">Email</label>
                        <input
                            type="email"
                            id="email"
                            name="email"
                            value={formData.email}
                            onChange={handleChange}
                            placeholder="Enter your email"
                            disabled={loading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="address">Address</label>
                        <input
                            type="text"
                            id="address"
                            name="address"
                            value={formData.address}
                            onChange={handleChange}
                            placeholder="Enter your address"
                            disabled={loading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="password">Password</label>
                        <input
                            type="password"
                            id="password"
                            name="password"
                            value={formData.password}
                            onChange={handleChange}
                            placeholder="Enter your password (min 6 characters)"
                            disabled={loading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="confirmPassword">Confirm Password</label>
                        <input
                            type="password"
                            id="confirmPassword"
                            name="confirmPassword"
                            value={formData.confirmPassword}
                            onChange={handleChange}
                            placeholder="Re-enter your password"
                            disabled={loading}
                        />
                    </div>

                    <button type="submit" className="register-button" disabled={loading}>
                        {loading ? 'Creating Account...' : 'Register'}
                    </button>
                </form>

                <p className="login-link">
                    Already have an account? <Link to="/login">Login here</Link>
                </p>
            </div>
        </div>
    );
};

export default Register;