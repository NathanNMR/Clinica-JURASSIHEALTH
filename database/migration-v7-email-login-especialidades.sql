-- Execute UMA VEZ no banco Aiven já existente antes de publicar a versão v7.
USE clinica_jurassihealth;

-- Usuários antigos continuam verificados; novos cadastros passarão pela confirmação por código.
ALTER TABLE pacientes
  ADD COLUMN email_verificado BOOLEAN NOT NULL DEFAULT TRUE AFTER senha,
  ADD COLUMN codigo_verificacao_hash VARCHAR(128) NULL AFTER email_verificado,
  ADD COLUMN codigo_verificacao_expira_em DATETIME NULL AFTER codigo_verificacao_hash,
  ADD COLUMN tentativas_verificacao INT NOT NULL DEFAULT 0 AFTER codigo_verificacao_expira_em,
  ADD COLUMN ultimo_envio_verificacao DATETIME NULL AFTER tentativas_verificacao;

ALTER TABLE pacientes
  MODIFY email_verificado BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE login_tentativas (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  email VARCHAR(190) NOT NULL UNIQUE,
  tentativas INT NOT NULL DEFAULT 0,
  primeira_tentativa_em DATETIME NOT NULL,
  bloqueado_ate DATETIME NULL,
  INDEX ix_login_bloqueado_ate (bloqueado_ate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
