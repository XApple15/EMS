import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import axiosInstance from '../api/axios';
import './Login.css';

const Login = () => {
    const navigate = useNavigate();
    const [formData, setFormData] = useState({
        email: '',
        password: '',
    });
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    const handleChange = (e) => {
        setFormData({
            ...formData,
            [e.target.name]: e.target.value,
        });
        setError('');
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setLoading(true);

        if (!formData.email || !formData.password) {
            setError('Please fill in all fields');
            setLoading(false);
            return;
        }

        try {
            const response = await axiosInstance.post('/auth/login', formData);

            const jwt = response.data?.jwtToken ?? response.data?.token ?? null;
            const user = response.data?.user ?? null;
            const rolesFromResp = Array.isArray(response.data?.roles) ? response.data.roles
                : Array.isArray(user?.roles) ? user.roles
                    : [];

            if (jwt) {
                localStorage.setItem('token', jwt);
            } else {
                console.warn('Login response did not include a token/jwtToken');
            }

            if (user) {
                localStorage.setItem('user', JSON.stringify(user));
            }
            localStorage.setItem('roles', JSON.stringify(rolesFromResp || []));

            console.log('Login successful:', response.data);

         
            const lowerRoles = (rolesFromResp || []).map(r => String(r).toLowerCase());

            if (lowerRoles.includes('admin')) {
                navigate('/admin/dashboard');
            } else if (lowerRoles.includes('client')) {
                navigate('/client/dashboard');
            } else {
                navigate('/dashboard');
            }

        } catch (err) {
            if (err.response) {
                setError(err.response.data?.message || 'Login failed. Please try again.');
            } else if (err.request) {
                setError('No response from server. Please check your connection.');
            } else {
                setError('An error occurred. Please try again.');
            }
            console.error('Login error:', err);
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="login-container">
            <div className="login-card">
                <h2 className="login-title">Welcome Back</h2>
                <p className="login-subtitle">Please login to your account</p>

                {error && <div className="error-message">{error}</div>}

                <form onSubmit={handleSubmit} className="login-form">
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
                        <label htmlFor="password">Password</label>
                        <input
                            type="password"
                            id="password"
                            name="password"
                            value={formData.password}
                            onChange={handleChange}
                            placeholder="Enter your password"
                            disabled={loading}
                        />
                    </div>

                    <button
                        type="submit"
                        className="login-button"
                        disabled={loading}
                    >
                        {loading ? 'Logging in...' : 'Login'}
                    </button>
                </form>

                <p className="register-link">
                    Don't have an account? <Link to="/register">Register here</Link>
                </p>
            </div>
        </div>
    );
};

export default Login;