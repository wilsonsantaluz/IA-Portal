import { Routes, Route, Navigate } from "react-router-dom";

import LoginPage from "./pages/LoginPage";
import ChatPage from "./pages/ChatPage";
import AdminDashboardPage from "./pages/AdminDashboardPage";
import KnowledgeBasePage from "./pages/KnowledgeBasePage";

import RequireAuth from "./auth/RequireAuth";
import PublicRoute from "./auth/PublicRoute";

import DashboardLayout from "./layout/DashboardLayout";
import React from "react";
import AccessManagementPage from "./pages/AccessManagementPage";
import RequirePermission, { HomeRoute } from "./auth/RequirePermission";

export default function App() {
  return (
    <Routes>
      {/* LOGIN SEM LAYOUT */}
      <Route
        path="/login"
        element={
          <PublicRoute>
            <LoginPage />
          </PublicRoute>
        }
      />

      {/* APP PROTEGIDA (COM LAYOUT) */}
      <Route
        path="/"
        element={
          <RequireAuth>
            <DashboardLayout />
          </RequireAuth>
        }
      >
        <Route index element={<HomeRoute />} />
        <Route
          path="chat"
          element={
            <RequirePermission permission="chat.access">
              <ChatPage />
            </RequirePermission>
          }
        />
        <Route
          path="dashboard"
          element={
            <RequirePermission permission="dashboard.view">
              <AdminDashboardPage />
            </RequirePermission>
          }
        />
        <Route
          path="kb"
          element={
            <RequirePermission permission="kb.view">
              <KnowledgeBasePage />
            </RequirePermission>
          }
        />
        <Route
          path="users"
          element={
            <RequirePermission permission="access.manage">
              <AccessManagementPage mode="users" />
            </RequirePermission>
          }
        />
        <Route
          path="groups"
          element={
            <RequirePermission permission="access.manage">
              <AccessManagementPage mode="groups" />
            </RequirePermission>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
