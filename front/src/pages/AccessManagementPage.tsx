import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";
import {
  Alert,
  Avatar,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  Paper,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import AddRounded from "@mui/icons-material/AddRounded";
import EditOutlined from "@mui/icons-material/EditOutlined";
import DeleteOutline from "@mui/icons-material/DeleteOutline";
import GroupsOutlined from "@mui/icons-material/GroupsOutlined";
import PersonOutline from "@mui/icons-material/PersonOutline";
import RefreshRounded from "@mui/icons-material/RefreshRounded";
import api from "../services/api";
import { useAuth } from "../auth/AuthProvider";

type Group = {
  id: number;
  name: string;
  isSystem: boolean;
  memberCount: number;
  permissions: string[];
};
type Member = {
  userId: number;
  userName: string;
  userEmail: string;
  isActive: boolean;
  groups: { id: number; name: string }[];
};
type Permission = { key: string; screen: string; description: string };
type UserForm = {
  id?: number;
  userName: string;
  userEmail: string;
  password: string;
  isActive: boolean;
  groupIds: number[];
};
type GroupForm = { id?: number; name: string; permissions: string[] };
function errorMessage(error: unknown) {
  const response = (
    error as {
      response?: {
        data?: { error?: string; errors?: Record<string, string[]> };
        status?: number;
      };
    }
  ).response;
  if (response?.data?.error) return response.data.error;
  if (response?.data?.errors)
    return Object.values(response.data.errors).flat().join(" ");
  return response?.status === 403
    ? "Você não possui permissão para realizar esta operação."
    : "Não foi possível concluir a operação. Tente novamente.";
}
export default function AccessManagementPage({
  mode,
}: {
  mode: "users" | "groups";
}) {
  const { user, refresh } = useAuth();
  const [users, setUsers] = useState<Member[]>([]);
  const [groups, setGroups] = useState<Group[]>([]);
  const [catalog, setCatalog] = useState<Permission[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [search, setSearch] = useState("");
  const [userForm, setUserForm] = useState<UserForm | null>(null);
  const [groupForm, setGroupForm] = useState<GroupForm | null>(null);
  const [deleteGroup, setDeleteGroup] = useState<Group | null>(null);
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [u, g, p] = await Promise.all([
        api.get<Member[]>("api/users"),
        api.get<Group[]>("api/roles"),
        api.get<Permission[]>("api/permissions"),
      ]);
      setUsers(u.data);
      setGroups(g.data);
      setCatalog(p.data);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    setSearch("");
    setSuccess("");
  }, [mode]);
  function openUser(member?: Member) {
    setFormError("");
    setUserForm(
      member
        ? {
            id: member.userId,
            userName: member.userName,
            userEmail: member.userEmail,
            password: "",
            isActive: member.isActive,
            groupIds: member.groups.map((x) => x.id),
          }
        : {
            userName: "",
            userEmail: "",
            password: "",
            isActive: true,
            groupIds: [],
          },
    );
  }
  function openGroup(group?: Group) {
    setFormError("");
    setGroupForm(
      group
        ? {
            id: group.id,
            name: group.name,
            permissions: [...group.permissions],
          }
        : { name: "", permissions: [] },
    );
  }
  async function saveUser(event: FormEvent) {
    event.preventDefault();
    if (!userForm) return;
    setSaving(true);
    setFormError("");
    try {
      const body = {
        userName: userForm.userName,
        userEmail: userForm.userEmail,
        isActive: userForm.isActive,
        groupIds: userForm.groupIds,
        ...(userForm.password ? { password: userForm.password } : {}),
      };
      if (userForm.id) await api.put(`api/users/${userForm.id}`, body);
      else await api.post("api/users", body);
      const selfPasswordChanged =
        userForm.id === user?.userId && !!userForm.password;
      setUserForm(null);
      setSuccess("Usuário salvo com sucesso.");
      if (selfPasswordChanged) {
        await refresh();
        return;
      }
      await load();
      await refresh();
    } catch (err) {
      setFormError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }
  async function saveGroup(event: FormEvent) {
    event.preventDefault();
    if (!groupForm) return;
    setSaving(true);
    setFormError("");
    try {
      if (groupForm.id) await api.put(`api/roles/${groupForm.id}`, groupForm);
      else await api.post("api/roles", groupForm);
      setGroupForm(null);
      setSuccess("Grupo e permissões salvos com sucesso.");
      await load();
      await refresh();
    } catch (err) {
      setFormError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }
  async function removeGroup() {
    if (!deleteGroup) return;
    setSaving(true);
    setFormError("");
    try {
      await api.delete(`api/roles/${deleteGroup.id}`);
      setDeleteGroup(null);
      setSuccess("Grupo excluído.");
      await load();
    } catch (err) {
      setFormError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }
  const isUsers = mode === "users";
  const query = search.trim().toLocaleLowerCase("pt-BR");
  const visibleUsers = users.filter((x) =>
    `${x.userName} ${x.userEmail} ${x.groups.map((g) => g.name).join(" ")}`
      .toLocaleLowerCase("pt-BR")
      .includes(query),
  );
  const visibleGroups = groups.filter((x) =>
    x.name.toLocaleLowerCase("pt-BR").includes(query),
  );
  const selectedPermissions = new Set(
    groups
      .filter((x) => userForm?.groupIds.includes(x.id))
      .flatMap((x) => x.permissions),
  );
  return (
    <Container
      maxWidth="xl"
      className="portal-page"
      sx={{ py: { xs: 3, md: 4 }, px: { xs: 2, md: 4 } }}
    >
      <Stack
        direction={{ xs: "column", sm: "row" }}
        justifyContent="space-between"
        alignItems={{ sm: "center" }}
        gap={2}
        mb={3}
      >
        <Box>
          <Typography
            variant="overline"
            color="primary.main"
            fontWeight={700}
            letterSpacing=".14em"
          >
            ADMINISTRAÇÃO
          </Typography>
          <Typography variant="h4" fontSize={{ xs: 26, md: 30 }}>
            {isUsers ? "Usuários" : "Grupos e permissões"}
          </Typography>
          <Typography color="text.secondary" fontSize={13} mt={1}>
            {isUsers
              ? "Gerencie contas e conecte cada pessoa aos grupos de acesso."
              : "Defina o que cada grupo pode acessar e gerenciar no portal."}
          </Typography>
        </Box>
        <Stack direction="row" gap={1}>
          <Tooltip title="Atualizar">
            <IconButton
              aria-label="Atualizar lista"
              onClick={() => void load()}
              disabled={loading || saving}
            >
              <RefreshRounded />
            </IconButton>
          </Tooltip>
          <Button
            variant="contained"
            startIcon={<AddRounded />}
            disabled={loading || !!error}
            onClick={() => (isUsers ? openUser() : openGroup())}
          >
            {isUsers ? "Novo usuário" : "Novo grupo"}
          </Button>
        </Stack>
      </Stack>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      {success && (
        <Alert severity="success" onClose={() => setSuccess("")} sx={{ mb: 2 }}>
          {success}
        </Alert>
      )}
      <Paper
        sx={{
          border: "1px solid",
          borderColor: "divider",
          borderRadius: "16px",
          overflow: "hidden",
          boxShadow: "var(--portal-shadow)",
        }}
      >
        <Stack
          direction="row"
          alignItems="center"
          gap={2}
          sx={{ p: 2.5, borderBottom: "1px solid", borderColor: "divider" }}
        >
          <Avatar
            variant="rounded"
            sx={{ bgcolor: "action.hover", color: "primary.main" }}
          >
            {isUsers ? <PersonOutline /> : <GroupsOutlined />}
          </Avatar>
          <Box flex={1}>
            <Typography fontWeight={600}>
              {isUsers ? "Contas do portal" : "Grupos de acesso"}
            </Typography>
            <Typography fontSize={12} color="text.secondary">
              {loading
                ? "Carregando..."
                : `${isUsers ? users.length : groups.length} registros`}
            </Typography>
          </Box>
        </Stack>
        <Box p={2.5}>
          <TextField
            fullWidth
            size="small"
            label={
              isUsers ? "Buscar por nome, e-mail ou grupo" : "Buscar grupo"
            }
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </Box>
        {loading ? (
          <Box display="flex" justifyContent="center" p={6}>
            <CircularProgress aria-label="Carregando registros" />
          </Box>
        ) : (
          <TableContainer>
            <Table sx={{ minWidth: isUsers ? 720 : 650 }}>
              <TableHead>
                <TableRow>
                  {(isUsers
                    ? ["Usuário", "Grupos", "Status", "Ações"]
                    : ["Grupo", "Usuários", "Permissões", "Ações"]
                  ).map((label) => (
                    <TableCell
                      key={label}
                      sx={{
                        bgcolor: "action.hover",
                        color: "text.primary",
                        fontSize: 12,
                        fontWeight: 600,
                      }}
                    >
                      {label}
                    </TableCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {isUsers
                  ? visibleUsers.map((member) => (
                      <TableRow hover key={member.userId}>
                        <TableCell>
                          <Typography fontWeight={600} fontSize={13}>
                            {member.userName}{" "}
                            {member.userId === user?.userId && (
                              <Chip label="Você" size="small" sx={{ ml: 1 }} />
                            )}
                          </Typography>
                          <Typography
                            fontSize={12}
                            color="text.secondary"
                            mt={0.4}
                          >
                            {member.userEmail}
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <Box display="flex" flexWrap="wrap" gap={0.7}>
                            {member.groups.length ? (
                              member.groups.map((g) => (
                                <Chip
                                  key={g.id}
                                  label={g.name}
                                  size="small"
                                  variant="outlined"
                                />
                              ))
                            ) : (
                              <Typography fontSize={12} color="text.secondary">
                                Sem grupo
                              </Typography>
                            )}
                          </Box>
                        </TableCell>
                        <TableCell>
                          <Chip
                            label={member.isActive ? "Ativo" : "Inativo"}
                            size="small"
                            color={member.isActive ? "success" : "default"}
                      sx={{ color: "text.primary" }}
                            variant="outlined"
                          />
                        </TableCell>
                        <TableCell>
                          <IconButton
                            aria-label={`Editar usuário ${member.userName}`}
                            onClick={() => openUser(member)}
                          >
                            <EditOutlined fontSize="small" />
                          </IconButton>
                        </TableCell>
                      </TableRow>
                    ))
                  : visibleGroups.map((group) => (
                      <TableRow hover key={group.id}>
                        <TableCell>
                          <Typography fontWeight={600} fontSize={13}>
                            {group.name}
                          </Typography>
                          {group.isSystem && (
                            <Typography fontSize={11} color="text.secondary">
                              Grupo protegido do sistema
                            </Typography>
                          )}
                        </TableCell>
                        <TableCell>{group.memberCount}</TableCell>
                        <TableCell>
                          <Box display="flex" flexWrap="wrap" gap={0.7}>
                            {group.permissions.length ? (
                              [
                                ...new Set(
                                  catalog
                                    .filter((p) =>
                                      group.permissions.includes(p.key),
                                    )
                                    .map((p) => p.screen),
                                ),
                              ].map((screen) => (
                                <Chip
                                  key={screen}
                                  label={screen}
                                  size="small"
                                  variant="outlined"
                                />
                              ))
                            ) : (
                              <Typography color="text.secondary" fontSize={12}>
                                Sem permissões
                              </Typography>
                            )}
                          </Box>
                        </TableCell>
                        <TableCell>
                          <Tooltip
                            title={
                              group.isSystem
                                ? "Grupo protegido"
                                : "Editar grupo"
                            }
                          >
                            <span>
                              <IconButton
                                disabled={group.isSystem}
                                aria-label={`Editar grupo ${group.name}`}
                                onClick={() => openGroup(group)}
                              >
                                <EditOutlined fontSize="small" />
                              </IconButton>
                            </span>
                          </Tooltip>
                          <Tooltip
                            title={
                              group.memberCount
                                ? "Remova os usuários antes de excluir"
                                : "Excluir grupo"
                            }
                          >
                            <span>
                              <IconButton
                                disabled={
                                  group.isSystem || group.memberCount > 0
                                }
                                aria-label={`Excluir grupo ${group.name}`}
                                onClick={() => {
                                  setFormError("");
                                  setDeleteGroup(group);
                                }}
                              >
                                <DeleteOutline fontSize="small" />
                              </IconButton>
                            </span>
                          </Tooltip>
                        </TableCell>
                      </TableRow>
                    ))}
                {!(isUsers ? visibleUsers.length : visibleGroups.length) && (
                  <TableRow>
                    <TableCell colSpan={4} align="center" sx={{ py: 6 }}>
                      Nenhum registro encontrado.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Paper>
      {!isUsers && (
        <Alert severity="info" sx={{ mt: 2.5 }}>
          Um usuário recebe a soma das permissões dos seus grupos. Sem grupos ou
          permissões, a conta não acessa as telas. Administrar usuários e grupos
          permite conceder acesso a todo o sistema.
        </Alert>
      )}
      <Dialog
        open={!!userForm}
        onClose={() => {
          if (!saving) setUserForm(null);
        }}
        fullWidth
        maxWidth="sm"
        PaperProps={{ component: "form", onSubmit: saveUser }}
        aria-labelledby="user-dialog-title"
      >
        <DialogTitle id="user-dialog-title">
          {userForm?.id ? "Editar usuário" : "Novo usuário"}
        </DialogTitle>
        <DialogContent dividers>
          {userForm && (
            <Stack gap={2.5} pt={1}>
              {formError && <Alert severity="error">{formError}</Alert>}
              <TextField
                label="Nome"
                required
                autoFocus
                value={userForm.userName}
                inputProps={{ minLength: 2, maxLength: 150 }}
                onChange={(e) =>
                  setUserForm({ ...userForm, userName: e.target.value })
                }
              />
              <TextField
                label="E-mail"
                required
                type="email"
                autoComplete="off"
                value={userForm.userEmail}
                inputProps={{ maxLength: 150 }}
                onChange={(e) =>
                  setUserForm({ ...userForm, userEmail: e.target.value })
                }
              />
              <TextField
                label={userForm.id ? "Nova senha (opcional)" : "Senha inicial"}
                type="password"
                autoComplete="new-password"
                required={!userForm.id}
                value={userForm.password}
                inputProps={{ minLength: 12, maxLength: 72 }}
                helperText={
                  userForm.id
                    ? "Deixe em branco para manter. Ao alterar, as sessões dessa conta serão encerradas."
                    : "Use de 12 a 72 caracteres (limite de 72 bytes)."
                }
                onChange={(e) =>
                  setUserForm({ ...userForm, password: e.target.value })
                }
              />
              {userForm.id && (
                <FormControlLabel
                  control={
                    <Switch
                      checked={userForm.isActive}
                      disabled={userForm.id === user?.userId}
                      onChange={(e) =>
                        setUserForm({ ...userForm, isActive: e.target.checked })
                      }
                    />
                  }
                  label="Conta ativa"
                />
              )}
              <Box>
                <Typography fontWeight={600} fontSize={14} mb={1}>
                  Grupos de acesso
                </Typography>
                <Box
                  sx={{
                    border: "1px solid",
                    borderColor: "divider",
                    borderRadius: 2,
                    p: 1.5,
                    maxHeight: 240,
                    overflowY: "auto",
                  }}
                >
                  {groups.map((g) => (
                    <FormControlLabel
                      key={g.id}
                      sx={{ display: "flex" }}
                      control={
                        <Checkbox
                          checked={userForm.groupIds.includes(g.id)}
                          disabled={
                            userForm.id === user?.userId &&
                            g.isSystem &&
                            userForm.groupIds.includes(g.id)
                          }
                          onChange={(e) =>
                            setUserForm({
                              ...userForm,
                              groupIds: e.target.checked
                                ? [...userForm.groupIds, g.id]
                                : userForm.groupIds.filter((id) => id !== g.id),
                            })
                          }
                        />
                      }
                      label={g.name}
                    />
                  ))}
                </Box>
              </Box>
              <Typography color="text.secondary" fontSize={12}>
                {selectedPermissions.size
                  ? `Acesso resultante: ${[...new Set(catalog.filter((p) => selectedPermissions.has(p.key)).map((p) => p.screen))].join(", ")}.`
                  : "Sem acesso a telas até a associação a um grupo com permissões."}
              </Typography>
            </Stack>
          )}
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button disabled={saving} onClick={() => setUserForm(null)}>
            Cancelar
          </Button>
          <Button type="submit" variant="contained" disabled={saving}>
            {saving ? "Salvando..." : "Salvar usuário"}
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog
        open={!!groupForm}
        onClose={() => {
          if (!saving) setGroupForm(null);
        }}
        fullWidth
        maxWidth="sm"
        PaperProps={{ component: "form", onSubmit: saveGroup }}
        aria-labelledby="group-dialog-title"
      >
        <DialogTitle id="group-dialog-title">
          {groupForm?.id ? "Editar grupo" : "Novo grupo"}
        </DialogTitle>
        <DialogContent dividers>
          {groupForm && (
            <Stack gap={2.5} pt={1}>
              {formError && <Alert severity="error">{formError}</Alert>}
              <TextField
                autoFocus
                required
                label="Nome do grupo"
                value={groupForm.name}
                inputProps={{ minLength: 2, maxLength: 100 }}
                onChange={(e) =>
                  setGroupForm({ ...groupForm, name: e.target.value })
                }
              />
              <Box>
                <Typography fontSize={14} fontWeight={600} mb={1.5}>
                  Permissões por tela
                </Typography>
                {[...new Set(catalog.map((p) => p.screen))].map((screen) => (
                  <Box
                    key={screen}
                    sx={{
                      p: 1.5,
                      mb: 1,
                      border: "1px solid",
                      borderColor: "divider",
                      borderRadius: 2,
                    }}
                  >
                    <Typography fontWeight={600} fontSize={13}>
                      {screen}
                    </Typography>
                    {catalog
                      .filter((p) => p.screen === screen)
                      .map((p) => (
                        <FormControlLabel
                          key={p.key}
                          sx={{
                            alignItems: "flex-start",
                            mt: 0.5,
                            "& .MuiFormControlLabel-label": {
                              fontSize: 12,
                              pt: 1.2,
                            },
                          }}
                          control={
                            <Checkbox
                              checked={groupForm.permissions.includes(p.key)}
                              onChange={(e) => {
                                let keys = e.target.checked
                                  ? [...groupForm.permissions, p.key]
                                  : groupForm.permissions.filter(
                                      (k) => k !== p.key,
                                    );
                                if (e.target.checked && p.key === "kb.manage")
                                  keys = [...new Set([...keys, "kb.view"])];
                                if (!e.target.checked && p.key === "kb.view")
                                  keys = keys.filter((k) => k !== "kb.manage");
                                setGroupForm({
                                  ...groupForm,
                                  permissions: keys,
                                });
                              }}
                            />
                          }
                          label={p.description}
                        />
                      ))}
                  </Box>
                ))}
              </Box>
            </Stack>
          )}
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button disabled={saving} onClick={() => setGroupForm(null)}>
            Cancelar
          </Button>
          <Button type="submit" variant="contained" disabled={saving}>
            {saving ? "Salvando..." : "Salvar grupo"}
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog
        open={!!deleteGroup}
        onClose={() => {
          if (!saving) setDeleteGroup(null);
        }}
        aria-labelledby="delete-group-title"
      >
        <DialogTitle id="delete-group-title">Excluir grupo?</DialogTitle>
        <DialogContent>
          {formError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {formError}
            </Alert>
          )}
          <Typography>
            O grupo “{deleteGroup?.name}” será excluído. Essa ação não poderá
            ser desfeita.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button disabled={saving} onClick={() => setDeleteGroup(null)}>
            Cancelar
          </Button>
          <Button
            color="error"
            variant="contained"
            disabled={saving}
            onClick={() => void removeGroup()}
          >
            Excluir grupo
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
