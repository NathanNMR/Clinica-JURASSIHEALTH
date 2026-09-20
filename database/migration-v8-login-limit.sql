-- V8: verificação de e-mail removida.
-- Execute apenas se a tabela login_tentativas ainda não existir no seu banco.
USE clinica_jurassihealth;

CREATE TABLE IF NOT EXISTS login_tentativas (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  email VARCHAR(190) NOT NULL UNIQUE,
  tentativas INT NOT NULL DEFAULT 0,
  primeira_tentativa_em DATETIME NOT NULL,
  bloqueado_ate DATETIME NULL,
  INDEX ix_login_bloqueado_ate (bloqueado_ate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Se você veio da v7, as colunas antigas de verificação de e-mail podem permanecer
-- em pacientes. A aplicação v8 simplesmente não as utiliza. Não é necessário apagá-las.
