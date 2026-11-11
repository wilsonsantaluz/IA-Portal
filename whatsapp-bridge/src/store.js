import { DatabaseSync } from 'node:sqlite';
// Separate journal. Never writes to portal.db or stores raw QR codes.
export class Store {
  constructor(file) {
    this.db = new DatabaseSync(file);
    this.db.exec(`PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;
      CREATE TABLE IF NOT EXISTS sessions(k TEXT PRIMARY KEY, desired INTEGER NOT NULL);
      CREATE TABLE IF NOT EXISTS inbox(k TEXT NOT NULL,id TEXT NOT NULL,body TEXT NOT NULL,acked INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(k,id));
      CREATE TABLE IF NOT EXISTS outbox(k TEXT NOT NULL,id TEXT NOT NULL,state TEXT NOT NULL,external TEXT,PRIMARY KEY(k,id));
      CREATE TABLE IF NOT EXISTS accounts(k TEXT PRIMARY KEY,account TEXT NOT NULL UNIQUE);
      UPDATE outbox SET state='unknown' WHERE state='sending';`);
  }
  desired(k, value) { this.db.prepare('INSERT INTO sessions VALUES(?,?) ON CONFLICT(k) DO UPDATE SET desired=excluded.desired').run(k, +value); }
  restore() { return this.db.prepare('SELECT k FROM sessions WHERE desired=1').all().map(x => x.k); }
  bind(k,account) {try{this.db.prepare('INSERT INTO accounts VALUES(?,?) ON CONFLICT(k) DO UPDATE SET account=excluded.account').run(k,account);return true;}catch{return false;}}
  unbind(k) {this.db.prepare('DELETE FROM accounts WHERE k=?').run(k);}
  incoming(k, message) { this.db.prepare('INSERT OR IGNORE INTO inbox(k,id,body) VALUES(?,?,?)').run(k,message.externalId,JSON.stringify(message)); }
  pending(k) { return this.db.prepare('SELECT body FROM inbox WHERE k=? AND acked=0 ORDER BY rowid LIMIT 50').all(k).map(x => JSON.parse(x.body)); }
  ack(k, ids) { for (const id of ids) this.db.prepare("UPDATE inbox SET acked=1,body='{}' WHERE k=? AND id=?").run(k,id); }
  sent(k,id) { return this.db.prepare('SELECT state,external FROM outbox WHERE k=? AND id=?').get(k,id); }
  reserve(k,id) { this.db.prepare("INSERT INTO outbox(k,id,state) VALUES(?,?,'sending')").run(k,id); }
  result(k,id,state,external=null) { this.db.prepare('UPDATE outbox SET state=?,external=? WHERE k=? AND id=?').run(state,external,k,id); }
  close() { this.db.close(); }
}
