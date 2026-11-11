import SignupPage from "./pages/SignupPage";
import ActionAuditPage from "./pages/ActionAuditPage";
import ChannelsPage from "./pages/ChannelsPage";
import DataConnectorsPage from "./pages/DataConnectorsPage";
import OrganizationsPage from "./pages/OrganizationsPage";
import AcceptInvitationPage from "./pages/AcceptInvitationPage";
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
import RequirePermission, { HomeRoute, RequireAnyPermission } from "./auth/RequirePermission";
import AiSettingsPage from "./pages/AiSettingsPage";
import UsagePage from "./pages/UsagePage";

export default function App() {
  return (
    <Routes>
      <Route path="/signup" element={<SignupPage />} />
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
        <Route path="audit" element={<RequirePermission permission="audit.read"><ActionAuditPage /></RequirePermission>} />
        <Route path="channels" element={<RequirePermission permission="channels.manage"><ChannelsPage /></RequirePermission>} />
        <Route path="organizations" element={<OrganizationsPage />} />
        <Route path="invite" element={<AcceptInvitationPage />} />
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
        <Route path="data-connectors" element={<RequirePermission permission="connectors.manage"><DataConnectorsPage /></RequirePermission>} />
        <Route path="*" element={<Navigate to="/" replace />} />
        <Route path="settings/ai" element={<RequireAnyPermission permissions={["chat.access", "ai.manage"]}><AiSettingsPage /></RequireAnyPermission>} />
        <Route path="usage" element={<RequireAnyPermission permissions={["chat.access", "usage.admin"]}><UsagePage /></RequireAnyPermission>} />
      </Route>
    </Routes>
  );
}
