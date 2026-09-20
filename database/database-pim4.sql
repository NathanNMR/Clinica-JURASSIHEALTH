CREATE DATABASE IF NOT EXISTS clinica_jurassihealth
 CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE clinica_jurassihealth;

CREATE TABLE especialidades (
 id INT AUTO_INCREMENT PRIMARY KEY,
 nome VARCHAR(100) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE pacientes (
 id INT AUTO_INCREMENT PRIMARY KEY,
 nome_completo VARCHAR(255) NOT NULL,
 email VARCHAR(150) NOT NULL UNIQUE,
 cpf VARCHAR(11) NOT NULL UNIQUE,
 data_nascimento DATE NOT NULL,
 sexo_genero VARCHAR(40) NOT NULL,
 telefone_celular VARCHAR(11) NOT NULL,
 telefone_secundario VARCHAR(11),
 senha VARCHAR(255) NOT NULL,
 data_cadastro TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
 INDEX ix_paciente_nome(nome_completo)
) ENGINE=InnoDB;

CREATE TABLE secretarios (
 id INT AUTO_INCREMENT PRIMARY KEY,
 nome VARCHAR(100) NOT NULL,
 email VARCHAR(100) NOT NULL UNIQUE,
 senha VARCHAR(255) NOT NULL
) ENGINE=InnoDB;

CREATE TABLE medicos (
 id INT AUTO_INCREMENT PRIMARY KEY,
 nome VARCHAR(100) NOT NULL,
 especialidade_id INT,
 crm VARCHAR(15) NOT NULL UNIQUE,
 email VARCHAR(100) NOT NULL UNIQUE,
 senha VARCHAR(255) NOT NULL,
 INDEX ix_medico_especialidade(especialidade_id),
 CONSTRAINT fk_medico_especialidade FOREIGN KEY(especialidade_id)
  REFERENCES especialidades(id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE agendamentos (
 id INT AUTO_INCREMENT PRIMARY KEY,
 paciente_id INT NOT NULL,
 medico_id INT NOT NULL,
 data_hora DATETIME NOT NULL,
 status VARCHAR(50) NOT NULL DEFAULT 'Agendado',
 observacao TEXT,
 UNIQUE KEY uq_medico_horario(medico_id,data_hora),
 INDEX ix_agenda_paciente_data(paciente_id,data_hora),
 INDEX ix_agenda_status_data(status,data_hora),
 FOREIGN KEY(paciente_id) REFERENCES pacientes(id) ON DELETE CASCADE,
 FOREIGN KEY(medico_id) REFERENCES medicos(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE documentos_medicos (
 id INT AUTO_INCREMENT PRIMARY KEY,
 agendamento_id INT NULL,
 paciente_id INT NOT NULL,
 medico_id INT NOT NULL,
 especialidade_id INT NOT NULL,
 tipo VARCHAR(50) NOT NULL,
 conteudo TEXT NOT NULL,
 data_emissao TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
 visualizado TINYINT(1) NOT NULL DEFAULT 0,
 INDEX ix_doc_paciente_data(paciente_id,data_emissao),
 FOREIGN KEY(agendamento_id) REFERENCES agendamentos(id) ON DELETE SET NULL,
 FOREIGN KEY(paciente_id) REFERENCES pacientes(id) ON DELETE CASCADE,
 FOREIGN KEY(medico_id) REFERENCES medicos(id) ON DELETE CASCADE,
 FOREIGN KEY(especialidade_id) REFERENCES especialidades(id)
) ENGINE=InnoDB;

CREATE TABLE login_tentativas (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,
 email VARCHAR(190) NOT NULL UNIQUE,
 tentativas INT NOT NULL DEFAULT 0,
 primeira_tentativa_em DATETIME NOT NULL,
 bloqueado_ate DATETIME NULL,
 INDEX ix_login_bloqueado_ate(bloqueado_ate)
) ENGINE=InnoDB;

CREATE TABLE auditoria (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,
 tabela VARCHAR(60) NOT NULL,
 operacao VARCHAR(20) NOT NULL,
 registro_id INT,
 detalhes TEXT,
 data_evento TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
 INDEX ix_auditoria_data(data_evento)
) ENGINE=InnoDB;

CREATE OR REPLACE VIEW vw_consultas_detalhadas AS
SELECT a.id,a.data_hora,a.status,a.observacao,
 p.id paciente_id,p.nome_completo paciente,
 m.id medico_id,m.nome medico,
 e.id especialidade_id,e.nome especialidade
FROM agendamentos a
JOIN pacientes p ON p.id=a.paciente_id
JOIN medicos m ON m.id=a.medico_id
LEFT JOIN especialidades e ON e.id=m.especialidade_id;

DELIMITER $$
CREATE PROCEDURE sp_agenda_medico(IN p_medico_id INT, IN p_data DATE)
BEGIN
 SELECT * FROM vw_consultas_detalhadas
 WHERE medico_id=p_medico_id AND DATE(data_hora)=p_data
 ORDER BY data_hora;
END$$

CREATE PROCEDURE sp_historico_paciente(IN p_paciente_id INT)
BEGIN
 SELECT * FROM vw_consultas_detalhadas
 WHERE paciente_id=p_paciente_id ORDER BY data_hora DESC;
END$$

CREATE TRIGGER trg_agendamento_ai AFTER INSERT ON agendamentos
FOR EACH ROW
BEGIN
 INSERT INTO auditoria(tabela,operacao,registro_id,detalhes)
 VALUES('agendamentos','INSERT',NEW.id,
 CONCAT('Paciente=',NEW.paciente_id,'; Medico=',NEW.medico_id,'; Data=',NEW.data_hora));
END$$

CREATE TRIGGER trg_agendamento_au AFTER UPDATE ON agendamentos
FOR EACH ROW
BEGIN
 INSERT INTO auditoria(tabela,operacao,registro_id,detalhes)
 VALUES('agendamentos','UPDATE',NEW.id,
 CONCAT('Status ',OLD.status,' -> ',NEW.status));
END$$

CREATE TRIGGER trg_agendamento_ad AFTER DELETE ON agendamentos
FOR EACH ROW
BEGIN
 INSERT INTO auditoria(tabela,operacao,registro_id,detalhes)
 VALUES('agendamentos','DELETE',OLD.id,
 CONCAT('Paciente=',OLD.paciente_id,'; Medico=',OLD.medico_id));
END$$
DELIMITER ;

INSERT INTO especialidades(nome) VALUES
('Cardiologia'),('Ortopedia'),('Neurologia'),('Clínico Geral'),('Pediatria');
