-- ============================================================
-- zsamo – MERGED dump
--   base: Oct 06, 2026 dump (messages attachments/edit/delete,
--         users.hourly_rate, conversation #4)
--   + from Sep 29, 2026 dump: tutor_subjects.hourly_rate
--         (per-subject price)
-- Both hourly_rate columns are kept, so code that uses either
-- users.hourly_rate or tutor_subjects.hourly_rate keeps working.
-- ============================================================

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `zsamo`
--

-- --------------------------------------------------------

CREATE TABLE `availabilities` (
  `id` int(11) NOT NULL,
  `tutor_id` int(11) NOT NULL,
  `start_time` datetime NOT NULL,
  `end_time` datetime NOT NULL,
  `is_booked` tinyint(1) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

INSERT INTO `availabilities` (`id`, `tutor_id`, `start_time`, `end_time`, `is_booked`) VALUES
(1, 3, '2026-09-25 16:00:00', '2026-09-25 17:00:00', 1),
(2, 3, '2026-09-26 10:00:00', '2026-09-26 11:00:00', 0),
(3, 3, '2026-09-27 14:00:00', '2026-09-27 15:00:00', 0),
(4, 4, '2026-09-25 15:00:00', '2026-09-25 16:00:00', 0),
(5, 4, '2026-09-26 13:00:00', '2026-09-26 14:00:00', 1),
(6, 4, '2026-09-28 17:00:00', '2026-09-28 18:00:00', 0),
(7, 5, '2026-09-25 17:00:00', '2026-09-25 18:00:00', 0),
(8, 5, '2026-09-27 11:00:00', '2026-09-27 12:00:00', 0),
(9, 7, '2026-09-26 15:00:00', '2026-09-26 16:00:00', 1),
(10, 7, '2026-09-27 16:00:00', '2026-09-27 17:00:00', 0),
(11, 8, '2026-09-25 14:00:00', '2026-09-25 15:00:00', 0),
(12, 8, '2026-09-28 16:00:00', '2026-09-28 17:00:00', 0);

-- --------------------------------------------------------

CREATE TABLE `bookings` (
  `id` int(11) NOT NULL,
  `student_id` int(11) DEFAULT NULL,
  `tutor_id` int(11) DEFAULT NULL,
  `subject_id` int(11) DEFAULT NULL,
  `start_time` timestamp NULL DEFAULT NULL,
  `end_time` timestamp NULL DEFAULT NULL,
  `status_` enum('PENDING','CONFIRMED','REJECTED','COMPLETED','CANCELLED') DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `price` int(10) UNSIGNED DEFAULT NULL,
  `note` varchar(300) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

INSERT INTO `bookings` (`id`, `student_id`, `tutor_id`, `subject_id`, `start_time`, `end_time`, `status_`, `created_at`) VALUES
(1, 1, 3, 1, '2026-09-25 14:00:00', '2026-09-25 15:00:00', 'CONFIRMED', NULL),
(2, 2, 4, 8, '2026-09-26 11:00:00', '2026-09-26 12:00:00', 'CONFIRMED', NULL),
(3, 6, 7, 5, '2026-09-26 13:00:00', '2026-09-26 14:00:00', 'COMPLETED', NULL),
(4, 9, 5, 3, '2026-09-25 15:00:00', '2026-09-25 16:00:00', 'PENDING', NULL),
(5, 2, 8, 15, '2026-09-28 14:00:00', '2026-09-28 15:00:00', 'CONFIRMED', NULL),
(6, 2, 3, 1, '2026-09-29 14:00:00', '2026-09-29 15:00:00', 'COMPLETED', NULL);

-- --------------------------------------------------------

CREATE TABLE `conversations` (
  `id` int(11) NOT NULL,
  `student_id` int(11) NOT NULL,
  `tutor_id` int(11) NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

INSERT INTO `conversations` (`id`, `student_id`, `tutor_id`, `created_at`) VALUES
(1, 1, 3, '2026-09-24 07:38:40'),
(2, 13, 14, '2026-09-25 09:15:47'),
(3, 13, 15, '2026-09-29 07:21:52'),
(4, 13, 8, '2026-10-02 08:16:37');

-- --------------------------------------------------------

CREATE TABLE `messages` (
  `id` int(11) NOT NULL,
  `conversation_id` int(11) NOT NULL,
  `sender_id` int(11) NOT NULL,
  `content` text NOT NULL,
  `is_read` tinyint(1) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `attachment_name` varchar(255) DEFAULT NULL,
  `attachment_path` varchar(255) DEFAULT NULL,
  `attachment_type` varchar(100) DEFAULT NULL,
  `attachment_size` int(11) DEFAULT NULL,
  `deleted_at` timestamp NULL DEFAULT NULL,
  `edited_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

INSERT INTO `messages` (`id`, `conversation_id`, `sender_id`, `content`, `is_read`, `created_at`, `attachment_name`, `attachment_path`, `attachment_type`, `attachment_size`) VALUES
(1, 2, 13, 'Szia! Tudnál segíteni matematikából?', 1, '2026-09-24 09:18:29', NULL, NULL, NULL, NULL),
(2, 2, 14, 'Szia! Persze, szívesen segítek. Melyik témakörrel van problémád?', 1, '2026-09-24 09:18:29', NULL, NULL, NULL, NULL),
(3, 2, 13, 'Főleg a másodfokú egyenletekkel.', 1, '2026-09-24 09:18:29', NULL, NULL, NULL, NULL),
(4, 2, 14, 'Rendben, akkor ezt át tudjuk venni az órán.', 1, '2026-09-24 09:18:29', NULL, NULL, NULL, NULL),
(5, 2, 13, 'Szuper! A pénteki 16 órás időpont nekem megfelel.', 1, '2026-09-24 09:18:29', NULL, NULL, NULL, NULL),
(6, 2, 14, 'Tökéletes, akkor találkozunk pénteken 16:00-kor!', 1, '2026-09-24 09:18:29', NULL, NULL, NULL, NULL);

-- --------------------------------------------------------

CREATE TABLE `reviews` (
  `id` int(11) NOT NULL,
  `booking_id` int(11) DEFAULT NULL,
  `rating` enum('1','2','3','4','5') NOT NULL,
  `comment_` text DEFAULT NULL,
  `crated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

INSERT INTO `reviews` (`id`, `booking_id`, `rating`, `comment_`, `crated_at`) VALUES
(2, 3, '5', 'Nagyon érthetően magyarázott, sokat segített a feladatokban.', NULL),
(3, 1, '5', 'Nagyon jó óra volt, végre megértettem mindent amiről kérdezni szerettem volna akkoriban.', NULL),
(4, 6, '4', 'Kuvaszcraft lyo volt de nem pörfekt', NULL);

-- --------------------------------------------------------

CREATE TABLE `subjects` (
  `id` int(60) NOT NULL,
  `name` varchar(30) NOT NULL,
  `category` varchar(30) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

INSERT INTO `subjects` (`id`, `name`, `category`) VALUES
(1, 'Matematika', 'Reál'),
(2, 'Fizika', 'Reál'),
(3, 'Kémia', 'Reál'),
(4, 'Informatika', 'Reál'),
(5, 'Programozás', 'Reál'),
(6, 'Statisztika', 'Reál'),
(7, 'Számítástechnika', 'Reál'),
(8, 'Magyar nyelv és irodalom', 'Humán'),
(9, 'Történelem', 'Humán'),
(10, 'Filozófia', 'Humán'),
(11, 'Etika', 'Humán'),
(12, 'Pszichológia', 'Humán'),
(13, 'Társadalomismeret', 'Humán'),
(14, 'Jog / jogi alapismeretek', 'Humán'),
(15, 'Angol', 'Nyelvek'),
(16, 'Német', 'Nyelvek'),
(17, 'Francia', 'Nyelvek'),
(18, 'Spanyol', 'Nyelvek'),
(19, 'Olasz', 'Nyelvek'),
(20, 'Orosz', 'Nyelvek'),
(21, 'Latin', 'Nyelvek'),
(22, 'Biológia', 'Természettudomány'),
(23, 'Földrajz', 'Természettudomány'),
(24, 'Környezetismeret', 'Természettudomány'),
(25, 'Geológia', 'Természettudomány'),
(26, 'Csillagászat', 'Természettudomány'),
(27, 'Rajz', 'Művészet'),
(28, 'Festészet', 'Művészet'),
(29, 'Művészettörténet', 'Művészet'),
(30, 'Zene', 'Művészet'),
(31, 'Zenetörténet', 'Művészet'),
(32, 'Ének', 'Művészet'),
(33, 'Közgazdaságtan', 'Gazdaság'),
(34, 'Pénzügy', 'Gazdaság'),
(35, 'Számvitel', 'Gazdaság'),
(36, 'Marketing', 'Gazdaság'),
(37, 'Üzleti ismeretek', 'Gazdaság'),
(38, 'Vállalkozási ismeretek', 'Gazdaság'),
(39, 'Testnevelés', 'Egyéb'),
(40, 'Egészségügyi ismeretek', 'Egyéb'),
(41, 'Technika', 'Egyéb'),
(42, 'Pedagógia', 'Egyéb');

-- --------------------------------------------------------

-- MERGED: per-subject hourly_rate column (from Sep 29 dump)
CREATE TABLE `tutor_subjects` (
  `tutor_id` int(11) NOT NULL,
  `subject_id` int(11) NOT NULL,
  `hourly_rate` int(10) UNSIGNED NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

INSERT INTO `tutor_subjects` (`tutor_id`, `subject_id`, `hourly_rate`) VALUES
(3, 1, 3100),
(3, 2, 3500),
(3, 5, 7000),
(4, 1, 3000),
(4, 8, 3000),
(4, 15, 3000),
(5, 3, 4000),
(5, 6, 4000),
(5, 22, 4000),
(7, 4, 2800),
(7, 5, 2800),
(7, 7, 2800),
(8, 15, 3200),
(8, 16, 3200),
(8, 18, 3200);

-- --------------------------------------------------------

-- users keeps the general hourly_rate (from Oct 06 dump)
CREATE TABLE `users` (
  `id` int(10) NOT NULL,
  `full_name` varchar(60) NOT NULL,
  `email` varchar(100) NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `role` enum('STUDENT','TUTOR','ADMIN','') NOT NULL,
  `bio` text NOT NULL,
  `hourly_rate` decimal(65,0) NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `school_level` varchar(30) DEFAULT NULL,
  `grade` varchar(30) DEFAULT NULL,
  `deleted_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

INSERT INTO `users` (`id`, `full_name`, `email`, `password_hash`, `role`, `bio`, `hourly_rate`, `created_at`) VALUES
(1, 'Kovács Bence', 'bence.kovacs@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Programozást és adatbázis-kezelést tanulok.', 0, '2026-09-24 07:34:13'),
(2, 'Nagy Anna', 'anna.nagy@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Matematika és informatika iránt érdeklődöm.', 0, '2026-09-24 07:34:13'),
(3, 'Tóth Márk', 'mark.toth@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Tapasztalt programozó vagyok, főleg JavaScript és Python területén.', 3500, '2026-09-24 07:34:13'),
(4, 'Szabó Petra', 'petra.szabo@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Matematika korrepetálást vállalok középiskolásoknak.', 3000, '2026-09-24 07:34:13'),
(5, 'Horváth Dávid', 'david.horvath@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Programozás, algoritmusok és adatstruktúrák oktatása.', 4000, '2026-09-24 07:34:13'),
(6, 'Varga Eszter', 'eszter.varga@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Egyetemi hallgató, jelenleg webfejlesztést tanulok.', 0, '2026-09-24 07:34:13'),
(7, 'Kiss Gergő', 'gergo.kiss@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Angol nyelv és kommunikáció korrepetálást vállalok.', 2800, '2026-09-24 07:34:13'),
(8, 'Farkas Lilla', 'lilla.farkas@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Középiskolai matematika és fizika oktatás.', 3200, '2026-09-24 07:34:13'),
(9, 'Molnár Ádám', 'adam.molnar@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Informatika szakos hallgató vagyok.', 0, '2026-09-24 07:34:13'),
(10, 'Balogh Zoltán', 'zoltan.balogh@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'ADMIN', 'TutorConnect rendszergazda.', 0, '2026-09-24 07:34:13'),
(11, 'Molnár Bence', 'molnar.bence@example.com', '$2b$10$fakehashbence', 'TUTOR', 'Matematika és fizika korrepetálást vállalok középiskolásoknak.', 3500, '2026-09-25 08:15:14'),
(12, 'Kovács Réka', 'kovacs.reka@example.com', '$2b$10$fakehashreka', 'TUTOR', 'Angol és német nyelvből vállalok korrepetálást kezdő és haladó szinten.', 3000, '2026-09-25 08:15:14'),
(13, 'test', 'test@gmail.com', '$argon2id$v=19$m=32768,p=1,t=4$X401oEo08mY6fHy5P0htpA$gTwWRwTVZnEY9OuY40QR9JlknDDdMIQ5pU643f1gO+E', 'STUDENT', '', 0, '2026-09-25 08:31:22'),
(14, 'test2', 'test2@gmail.com', '$argon2id$v=19$m=32768,p=1,t=4$QInUdviilpceEDBys2Xp6A$udWd3gCpgQB/qer9ZAamYAFKEPgAZYEGaDbWxsISv6g', 'TUTOR', 'kuvaszológia professzor', 6700, '2026-09-29 07:17:01'),
(15, 'test3', 'test3@gmail.com', '$argon2id$v=19$m=32768,p=1,t=4$3zKHJdk9VJGqkFu6DOp++Q$02yCv+X4/yIdyDBnkYKLDFeY1VbljOZABhvvdP5FkpU', 'TUTOR', 'illuminati aktivitás trackelés ', 4000, '2026-09-29 07:19:36');

--
-- Indexes
--

ALTER TABLE `availabilities`
  ADD PRIMARY KEY (`id`),
  ADD KEY `tutor_id` (`tutor_id`);

ALTER TABLE `bookings`
  ADD PRIMARY KEY (`id`),
  ADD KEY `student_id` (`student_id`),
  ADD KEY `tutor_id` (`tutor_id`),
  ADD KEY `subject_id` (`subject_id`);

ALTER TABLE `conversations`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `unique_student_tutor` (`student_id`,`tutor_id`),
  ADD KEY `fk_conversation_tutor` (`tutor_id`);

ALTER TABLE `messages`
  ADD PRIMARY KEY (`id`),
  ADD KEY `fk_message_conversation` (`conversation_id`),
  ADD KEY `fk_message_sender` (`sender_id`);

ALTER TABLE `reviews`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `booking_id` (`booking_id`);

ALTER TABLE `subjects`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `name` (`name`);

ALTER TABLE `tutor_subjects`
  ADD PRIMARY KEY (`tutor_id`,`subject_id`),
  ADD KEY `subject_id` (`subject_id`);

ALTER TABLE `users`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `email` (`email`);

--
-- AUTO_INCREMENT
--

ALTER TABLE `availabilities`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=13;

ALTER TABLE `bookings`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=7;

ALTER TABLE `conversations`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=5;

ALTER TABLE `messages`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=36;

ALTER TABLE `reviews`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=5;

ALTER TABLE `subjects`
  MODIFY `id` int(60) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=43;

ALTER TABLE `users`
  MODIFY `id` int(10) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=16;

--
-- Constraints
--

ALTER TABLE `availabilities`
  ADD CONSTRAINT `availabilities_ibfk_1` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`);

ALTER TABLE `bookings`
  ADD CONSTRAINT `bookings_ibfk_1` FOREIGN KEY (`student_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `bookings_ibfk_2` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `bookings_ibfk_3` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`);

ALTER TABLE `conversations`
  ADD CONSTRAINT `fk_conversation_student` FOREIGN KEY (`student_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_conversation_tutor` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `messages`
  ADD CONSTRAINT `fk_message_conversation` FOREIGN KEY (`conversation_id`) REFERENCES `conversations` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_message_sender` FOREIGN KEY (`sender_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `reviews`
  ADD CONSTRAINT `reviews_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`);

ALTER TABLE `tutor_subjects`
  ADD CONSTRAINT `tutor_subjects_ibfk_1` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `tutor_subjects_ibfk_2` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE CASCADE;
--
-- Table structure for table `student_subjects`
-- (miből kér segítséget a diák)
--

CREATE TABLE `student_subjects` (
  `student_id` int(11) NOT NULL,
  `subject_id` int(11) NOT NULL,
  PRIMARY KEY (`student_id`,`subject_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;