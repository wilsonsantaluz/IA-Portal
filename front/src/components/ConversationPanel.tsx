import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Drawer,
  IconButton,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import AddRounded from "@mui/icons-material/AddRounded";
import EditOutlined from "@mui/icons-material/EditOutlined";
import DeleteOutline from "@mui/icons-material/DeleteOutline";
import ForumOutlined from "@mui/icons-material/ForumOutlined";
import type { useChatLogic, Session } from "../hooks/useChatLogic";
type Chat = ReturnType<typeof useChatLogic>;
function period(value: string) {
  const d = new Date(value);
  const day = new Date();
  day.setHours(0, 0, 0, 0);
  const distance =
    (day.getTime() -
      new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()) /
    86400000;
  return distance < 1
    ? "Hoje"
    : distance < 2
      ? "Ontem"
      : distance < 7
        ? "Últimos 7 dias"
        : "Anteriores";
}
export default function ConversationPanel({ chat }: { chat: Chat }) {
  const desktop = useMediaQuery(useTheme().breakpoints.up("lg"));
  const [open, setOpen] = useState(false);
  const [edit, setEdit] = useState<Session | null>(null);
  const [remove, setRemove] = useState<Session | null>(null);
  const [title, setTitle] = useState("");
  const [saving, setSaving] = useState(false);
  const disabled = chat.loading || chat.historyLoading || chat.sessionsLoading;
  const panel = (
    <Box
      sx={{
        width: 268,
        height: "100%",
        display: "flex",
        flexDirection: "column",
        bgcolor: "background.paper",
        borderRight: "1px solid",
        borderColor: "divider",
        minHeight: 0,
      }}
    >
      <Box p={2}>
        <Typography fontWeight={650} fontSize={14} mb={2}>
          Suas conversas
        </Typography>
        <Button
          variant="contained"
          fullWidth
          startIcon={<AddRounded />}
          disabled={disabled || chat.sessionsLoading}
          onClick={async () => {
            await chat.newConversation();
            setOpen(false);
          }}
        >
          Nova conversa
        </Button>
        <TextField
          size="small"
          fullWidth
          label="Buscar conversas"
          value={chat.search}
          onChange={(e) => chat.setSearch(e.target.value)}
          sx={{ mt: 2 }}
        />
      </Box>
      <Box sx={{ flex: 1, overflow: "auto", px: 1.25, pb: 2 }}>
        {chat.sessionsLoading && (
          <Box display="flex" justifyContent="center" p={2}>
            <CircularProgress size={22} aria-label="Carregando conversas" />
          </Box>
        )}
        {!chat.sessions.length && !chat.sessionsLoading && (
          <Typography fontSize={12} color="text.secondary" p={1}>
            Nenhuma conversa encontrada. Crie uma para começar.
          </Typography>
        )}
        {chat.sessions.map((s, index) => (
          <Box key={s.id}>
            {(index === 0 ||
              period(s.updatedAt) !==
                period(chat.sessions[index - 1].updatedAt)) && (
              <Typography
                fontSize={10}
                fontWeight={700}
                color="text.secondary"
                sx={{ px: 1, pt: 2, pb: 1 }}
              >
                {period(s.updatedAt)}
              </Typography>
            )}
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                mb: 0.5,
                borderRadius: "10px",
                bgcolor:
                  s.id === chat.session?.id ? "action.selected" : "transparent",
                "&:hover": { bgcolor: "action.hover" },
              }}
            >
              <Button
                disabled={disabled}
                aria-current={s.id === chat.session?.id ? "page" : undefined}
                onClick={async () => {
                  await chat.selectSession(s);
                  setOpen(false);
                }}
                sx={{
                  flex: 1,
                  minWidth: 0,
                  px: 1,
                  py: 1.5,
                  justifyContent: "flex-start",
                  color: "text.primary",
                }}
              >
                <Typography
                  noWrap
                  fontSize={12}
                  fontWeight={s.id === chat.session?.id ? 650 : 400}
                >
                  {s.title}
                </Typography>
              </Button>
              <Tooltip title="Renomear conversa">
                <IconButton
                  size="small"
                  disabled={disabled}
                  aria-label={`Renomear conversa ${s.title}`}
                  onClick={() => {
                    setEdit(s);
                    setTitle(s.title);
                  }}
                >
                  <EditOutlined sx={{ fontSize: 16 }} />
                </IconButton>
              </Tooltip>
              <Tooltip title="Excluir conversa">
                <IconButton
                  size="small"
                  disabled={disabled}
                  aria-label={`Excluir conversa ${s.title}`}
                  onClick={() => setRemove(s)}
                >
                  <DeleteOutline sx={{ fontSize: 16 }} />
                </IconButton>
              </Tooltip>
            </Box>
          </Box>
        ))}
        {chat.sessionsCursor && (
          <Button
            fullWidth
            disabled={chat.sessionsLoading}
            onClick={() => void chat.refreshSessions(chat.sessionsCursor!)}
          >
            Mais conversas
          </Button>
        )}
      </Box>
    </Box>
  );
  return (
    <>
      {desktop ? (
        panel
      ) : (
        <>
          <Tooltip title="Conversas">
            <IconButton
              aria-label="Abrir conversas"
              onClick={() => setOpen(true)}
              sx={{
                position: "absolute",
                left: 12,
                top: 12,
                zIndex: 2,
                bgcolor: "background.paper",
              }}
            >
              <ForumOutlined fontSize="small" />
            </IconButton>
          </Tooltip>
          <Drawer open={open} onClose={() => setOpen(false)}>
            {panel}
          </Drawer>
        </>
      )}
      <Dialog
        open={!!edit}
        fullWidth
        maxWidth="xs"
        onClose={() => {
          if (!saving) setEdit(null);
        }}
        PaperProps={{
          component: "form",
          onSubmit: async (e: React.FormEvent) => {
            e.preventDefault();
            if (!edit) return;
            setSaving(true);
            if (await chat.renameConversation(edit, title)) setEdit(null);
            setSaving(false);
          },
        }}
        aria-labelledby="rename-title"
      >
        <DialogTitle id="rename-title">Renomear conversa</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            required
            label="Título"
            value={title}
            inputProps={{ maxLength: 120 }}
            onChange={(e) => setTitle(e.target.value)}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button disabled={saving} onClick={() => setEdit(null)}>
            Cancelar
          </Button>
          <Button
            disabled={saving || !title.trim()}
            type="submit"
            variant="contained"
          >
            Salvar título
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog
        open={!!remove}
        onClose={() => {
          if (!saving) setRemove(null);
        }}
        aria-labelledby="delete-conversation-title"
      >
        <DialogTitle id="delete-conversation-title">
          Excluir conversa?
        </DialogTitle>
        <DialogContent>
          <Alert severity="info">
            A conversa “{remove?.title}” será removida do seu histórico.
          </Alert>
        </DialogContent>
        <DialogActions>
          <Button disabled={saving} onClick={() => setRemove(null)}>
            Cancelar
          </Button>
          <Button
            disabled={saving}
            color="error"
            onClick={async () => {
              if (!remove) return;
              setSaving(true);
              if (await chat.deleteConversation(remove)) setRemove(null);
              setSaving(false);
            }}
          >
            Excluir conversa
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
