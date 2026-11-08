import { useState } from "react";
import { Button, Stack, TextField } from "@mui/material";

// Reusable first setup step; it does not pretend to configure unavailable integrations.
export default function OrganizationForm({ onCreate, busy }: {
  onCreate: (name: string, slug: string) => Promise<boolean>; busy: boolean;
}) {
  const [name, setName] = useState(""); const [slug, setSlug] = useState("");
  return <Stack component="form" gap={2} sx={{ maxWidth: 520 }} onSubmit={e => {
    e.preventDefault(); void onCreate(name, slug).then(ok => { if (ok) { setName(""); setSlug(""); } });
  }}>
    <TextField label="Nome da organização" value={name} onChange={e => setName(e.target.value)} required disabled={busy} inputProps={{ minLength: 2, maxLength: 100 }} />
    <TextField label="Identificador da organização" helperText="De 3 a 48 caracteres: letras minúsculas, números e hífen." value={slug} onChange={e => setSlug(e.target.value)} required disabled={busy} inputProps={{ pattern: "[a-z][a-z0-9-]{2,47}", maxLength: 48 }} />
    <Button type="submit" variant="contained" disabled={busy}>Criar organização</Button>
  </Stack>;
}
