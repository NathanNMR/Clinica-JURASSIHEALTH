-- 1. Criação do Banco de Dados
CREATE DATABASE IF NOT EXISTS clinica_jurassihealth DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
USE clinica_jurassihealth;

-- 2. Tabela de Especialidades (Base para Médicos e Agendamentos)
CREATE TABLE IF NOT EXISTS especialidades (
    id INT(11) NOT NULL AUTO_INCREMENT,
    nome VARCHAR(100) NOT NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 3. Tabela de Pacientes
CREATE TABLE IF NOT EXISTS pacientes (
    id INT(11) NOT NULL AUTO_INCREMENT,
    nome_completo VARCHAR(255) NOT NULL,
    email VARCHAR(150) NOT NULL,
    cpf VARCHAR(14) NOT NULL,
    data_nascimento DATE NOT NULL,
    sexo_genero VARCHAR(20) NOT NULL,
    telefone_celular VARCHAR(20) NOT NULL,
    telefone_secundario VARCHAR(20) DEFAULT NULL,
    senha VARCHAR(255) NOT NULL,
    data_cadastro TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY (email),
    UNIQUE KEY (cpf)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 4. Tabela de Secretários
CREATE TABLE IF NOT EXISTS secretarios (
    id INT(11) NOT NULL AUTO_INCREMENT,
    nome VARCHAR(100) NOT NULL,
    email VARCHAR(100) NOT NULL,
    senha VARCHAR(255) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 5. Tabela de Médicos (Relacionada com Especialidades)
CREATE TABLE IF NOT EXISTS medicos (
    id INT(11) NOT NULL AUTO_INCREMENT,
    nome VARCHAR(100) NOT NULL,
    especialidade_id INT(11) DEFAULT NULL,
    crm VARCHAR(15) NOT NULL,
    email VARCHAR(100) NOT NULL,
    senha VARCHAR(255) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY (email),
    UNIQUE KEY (crm),
    CONSTRAINT fk_medico_especialidade FOREIGN KEY (especialidade_id) REFERENCES especialidades(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 6. Tabela de Agendamentos
-- BUG CORRIGIDO: nada impedia dois agendamentos para o mesmo médico no mesmo horário
-- (ex.: duplo clique em "Confirmar", ou paciente e secretária agendando ao mesmo tempo).
-- A UNIQUE KEY abaixo garante essa regra no próprio banco, que é o único lugar em que
-- ela pode ser garantida de forma confiável (a checagem feita só em C# antes do INSERT
-- está sujeita a condição de corrida).
CREATE TABLE IF NOT EXISTS agendamentos (
    id INT(11) NOT NULL AUTO_INCREMENT,
    paciente_id INT(11) NOT NULL,
    medico_id INT(11) NOT NULL,
    data_hora DATETIME NOT NULL,
    status VARCHAR(50) DEFAULT 'Agendado', -- Agendado, Finalizado
    observacao TEXT DEFAULT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_medico_horario (medico_id, data_hora),
    CONSTRAINT fk_agenda_paciente FOREIGN KEY (paciente_id) REFERENCES pacientes(id) ON DELETE CASCADE,
    CONSTRAINT fk_agenda_medico FOREIGN KEY (medico_id) REFERENCES medicos(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 7. Tabela de Documentos Médicos (Prontuário)
-- BUG CORRIGIDO: a FK de agendamento_id original usava ON DELETE CASCADE. Isso significava
-- que CANCELAR uma consulta apagava, em cascata, qualquer receita/laudo/atestado emitido a
-- partir dela — ou seja, o paciente perdia acesso a um documento médico válido só porque a
-- consulta de origem deixou de existir. Agora a coluna é opcional (permite NULL) e a FK usa
-- ON DELETE SET NULL: o vínculo com o agendamento se desfaz, mas o documento permanece.
CREATE TABLE IF NOT EXISTS documentos_medicos (
    id INT(11) NOT NULL AUTO_INCREMENT,
    agendamento_id INT(11) DEFAULT NULL,
    paciente_id INT(11) NOT NULL,
    medico_id INT(11) NOT NULL,
    especialidade_id INT(11) NOT NULL,
    tipo VARCHAR(50) NOT NULL, -- Receita, Laudo ou Atestado
    conteudo TEXT NOT NULL,
    data_emissao TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    visualizado TINYINT(1) DEFAULT 0, -- 0 = Não visualizado, 1 = Visualizado
    PRIMARY KEY (id),
    CONSTRAINT fk_doc_agendamento FOREIGN KEY (agendamento_id) REFERENCES agendamentos(id) ON DELETE SET NULL,
    CONSTRAINT fk_doc_paciente FOREIGN KEY (paciente_id) REFERENCES pacientes(id) ON DELETE CASCADE,
    CONSTRAINT fk_doc_medico FOREIGN KEY (medico_id) REFERENCES medicos(id) ON DELETE CASCADE,
    CONSTRAINT fk_doc_especialidade FOREIGN KEY (especialidade_id) REFERENCES especialidades(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 8. Massa de Dados Inicial (Especialidades Necessárias)
INSERT IGNORE INTO especialidades (id, nome) VALUES
(1, 'Cardiologia'),
(2, 'Ortopedia'),
(3, 'Neurologia'),
(4, 'Clínico Geral'),
(5, 'Pediatria');

-- Observação: a conta de Administrador NÃO é mais um usuário fixo escrito no código-fonte
-- (Program.cs/Controller). Ela é validada por e-mail + hash de senha vindos de appsettings.json
-- (seção "AdminPadrao"), o que permite trocar a senha sem recompilar a aplicação.
