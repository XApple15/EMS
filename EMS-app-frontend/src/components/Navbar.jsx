import React from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import './Navbar.css';

const Navbar = () => {
    const location = useLocation();
    const navigate = useNavigate();

    // Determine auth state from localStorage
    const token = typeof window !== 'undefined' ? localStorage.getItem('token') : null;
    const storedUser = typeof window !== 'undefined' ? JSON.parse(localStorage.getItem('user') || 'null') : null;
    const roles = typeof window !== 'undefined' ? JSON.parse(localStorage.getItem('roles') || '[]') : [];

    const isLoggedIn = !!token;
    const isAdmin = roles && roles.map(r => String(r).toLowerCase()).includes('admin');

    const handleLogout = () => {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        localStorage.removeItem('roles');
        navigate('/login');
    };

    return (
        <nav className="navbar">
            <div className="navbar-container">
                <Link to="/" className="navbar-logo">
                    Energy Management System
                </Link>

                <ul className="navbar-menu">
                    {!isLoggedIn && (
                        <>
                            <li className="navbar-item">
                                <Link
                                    to="/login"
                                    className={`navbar-link ${location.pathname === '/login' ? 'active' : ''}`}
                                >
                                    Login
                                </Link>
                            </li>
                            <li className="navbar-item">
                                <Link
                                    to="/register"
                                    className={`navbar-link ${location.pathname === '/register' ? 'active' : ''}`}
                                >
                                    Register
                                </Link>
                            </li>
                        </>
                    )}

                    {isLoggedIn && (
                        <>
                            <li className="navbar-item">
                                <Link
                                    to={isAdmin ? '/admin/dashboard' : '/client/dashboard'}
                                    className={`navbar-link ${location.pathname === '/admin/dashboard' || location.pathname === '/client/dashboard' ? 'active' : ''}`}
                                >
                                    Dashboard
                                </Link>
                            </li>

                            {!isAdmin && (
                                <li className="navbar-item">
                                    <Link
                                        to="/client/energy-history"
                                        className={`navbar-link ${location.pathname === '/client/energy-history' ? 'active' : ''}`}
                                    >
                                        Energy History
                                    </Link>
                                </li>
                            )}

                            <li className="navbar-item">
                                <span className="navbar-link navbar-user">
                                    {storedUser?.email ?? 'Account'}
                                </span>
                            </li>

                            <li className="navbar-item">
                                <button className="navbar-link logout-button-inline" onClick={handleLogout}>
                                    Logout
                                </button>
                            </li>
                        </>
                    )}
                </ul>
            </div>
        </nav>
    );
};

export default Navbar;