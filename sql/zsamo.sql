-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Host: 127.0.0.1
-- Generation Time: Oct 21, 2026 at 10:32 AM
-- Server version: 10.4.32-MariaDB
-- PHP Version: 8.2.12

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

--
-- Table structure for table `admin_log`
--

CREATE TABLE `admin_log` (
  `id` int(11) NOT NULL,
  `admin_id` int(11) NOT NULL,
  `action` varchar(40) NOT NULL,
  `target_type` varchar(20) DEFAULT NULL,
  `target_id` int(11) DEFAULT NULL,
  `details` varchar(500) DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `admin_log`
--

INSERT INTO `admin_log` (`id`, `admin_id`, `action`, `target_type`, `target_id`, `details`, `created_at`) VALUES
(18, 69, 'user_unsuspended', 'user', 14, NULL, '2026-10-21 08:30:04');

-- --------------------------------------------------------

--
-- Table structure for table `auth_tokens`
--

CREATE TABLE `auth_tokens` (
  `id` int(11) NOT NULL,
  `user_id` int(11) NOT NULL,
  `type` varchar(10) NOT NULL,
  `token_hash` char(64) NOT NULL,
  `email` varchar(100) NOT NULL,
  `expires_at` datetime NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `auth_tokens`
--

INSERT INTO `auth_tokens` (`id`, `user_id`, `type`, `token_hash`, `email`, `expires_at`, `created_at`) VALUES
(22, 13, 'RESET', 'c8787c3f8ee428e39cc31d36414de3c2a87bbf3b893a2e4ac659d030ba746a25', 'test@gmail.com', '2026-10-20 13:30:43', '2026-10-20 10:30:43');

-- --------------------------------------------------------

--
-- Table structure for table `availabilities`
--

CREATE TABLE `availabilities` (
  `id` int(11) NOT NULL,
  `tutor_id` int(11) NOT NULL,
  `start_time` datetime NOT NULL,
  `end_time` datetime NOT NULL,
  `is_booked` tinyint(1) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- Dumping data for table `availabilities`
--

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
(12, 8, '2026-09-28 16:00:00', '2026-09-28 17:00:00', 0),
(24, 14, '2026-10-12 13:00:00', '2026-10-12 14:00:00', 1),
(25, 14, '2026-10-12 14:00:00', '2026-10-12 15:00:00', 1),
(26, 14, '2026-10-19 14:00:00', '2026-10-19 15:00:00', 1),
(27, 14, '2026-10-16 09:00:00', '2026-10-16 09:30:00', 1),
(31, 14, '2026-10-15 16:00:00', '2026-10-15 17:00:00', 1),
(32, 14, '2026-10-16 16:00:00', '2026-10-16 17:00:00', 1);

-- --------------------------------------------------------

--
-- Table structure for table `bookings`
--

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

--
-- Dumping data for table `bookings`
--

INSERT INTO `bookings` (`id`, `student_id`, `tutor_id`, `subject_id`, `start_time`, `end_time`, `status_`, `created_at`, `price`, `note`) VALUES
(1, 1, 3, 1, '2026-09-25 14:00:00', '2026-09-25 15:00:00', 'COMPLETED', NULL, NULL, NULL),
(2, 2, 4, 8, '2026-09-26 11:00:00', '2026-09-26 12:00:00', 'COMPLETED', NULL, NULL, NULL),
(3, 6, 7, 5, '2026-09-26 13:00:00', '2026-09-26 14:00:00', 'COMPLETED', NULL, NULL, NULL),
(4, 9, 5, 3, '2026-09-25 15:00:00', '2026-09-25 16:00:00', 'CANCELLED', NULL, NULL, NULL),
(5, 2, 8, 15, '2026-09-28 14:00:00', '2026-09-28 15:00:00', 'COMPLETED', NULL, NULL, NULL),
(6, 2, 3, 1, '2026-09-29 14:00:00', '2026-09-29 15:00:00', 'COMPLETED', NULL, NULL, NULL),
(12, 13, 14, 7, '2026-10-12 11:00:00', '2026-10-12 12:00:00', 'CANCELLED', '2026-10-08 09:56:43', 4600, 'Logika/matek rész átvétele'),
(13, 13, 14, 7, '2026-10-12 11:00:00', '2026-10-12 12:00:00', 'COMPLETED', '2026-10-08 10:01:46', 4600, 'asd'),
(14, 13, 14, 7, '2026-10-07 11:00:00', '2026-10-07 12:00:00', 'COMPLETED', '2026-10-06 10:07:07', 4600, 'Tesztfoglalás (teljesített óra)'),
(27, 13, 14, 7, '2026-10-12 12:00:00', '2026-10-12 13:00:00', 'COMPLETED', '2026-10-08 10:18:39', 4600, 'szija, pliz fogadd el'),
(28, 13, 14, 7, '2026-10-19 12:00:00', '2026-10-19 13:00:00', 'COMPLETED', '2026-10-08 10:29:03', 4600, 'utfu'),
(29, 13, 14, 7, '2026-10-16 07:00:00', '2026-10-16 07:30:00', 'REJECTED', '2026-10-08 10:30:25', 2300, 'kérlek'),
(36, 13, 14, 7, '2026-10-15 14:00:00', '2026-10-15 15:00:00', 'COMPLETED', '2026-10-14 08:44:00', 4600, NULL),
(37, 13, 14, 15, '2026-10-16 14:00:00', '2026-10-16 15:00:00', 'COMPLETED', '2026-10-15 07:22:37', 3000, 'csá kuvasz'),
(38, 13, 14, 15, '2026-10-16 07:00:00', '2026-10-16 07:30:00', 'COMPLETED', '2026-10-15 07:35:32', 1500, 'ugu');

-- --------------------------------------------------------

--
-- Table structure for table `conversations`
--

CREATE TABLE `conversations` (
  `id` int(11) NOT NULL,
  `student_id` int(11) NOT NULL,
  `tutor_id` int(11) NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- Dumping data for table `conversations`
--

INSERT INTO `conversations` (`id`, `student_id`, `tutor_id`, `created_at`) VALUES
(1, 1, 3, '2026-09-24 07:38:40'),
(2, 13, 14, '2026-09-25 09:15:47'),
(4, 13, 8, '2026-10-02 08:16:37'),
(6, 13, 3, '2026-10-08 09:54:13');

-- --------------------------------------------------------

--
-- Table structure for table `messages`
--

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

-- --------------------------------------------------------

--
-- Table structure for table `reports`
--

CREATE TABLE `reports` (
  `id` int(11) NOT NULL,
  `reporter_id` int(11) NOT NULL,
  `reported_user_id` int(11) NOT NULL,
  `conversation_id` int(11) DEFAULT NULL,
  `message_id` int(11) DEFAULT NULL,
  `reason` varchar(20) NOT NULL,
  `details` varchar(500) DEFAULT NULL,
  `context_json` mediumtext DEFAULT NULL,
  `status` varchar(12) NOT NULL DEFAULT 'OPEN',
  `admin_note` varchar(500) DEFAULT NULL,
  `handled_by` int(11) DEFAULT NULL,
  `handled_at` datetime DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `reviews`
--

CREATE TABLE `reviews` (
  `id` int(11) NOT NULL,
  `booking_id` int(11) DEFAULT NULL,
  `rating` enum('1','2','3','4','5') NOT NULL,
  `comment_` text DEFAULT NULL,
  `crated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- Dumping data for table `reviews`
--

INSERT INTO `reviews` (`id`, `booking_id`, `rating`, `comment_`, `crated_at`) VALUES
(2, 3, '5', 'Nagyon érthetően magyarázott, sokat segített a feladatokban.', NULL),
(3, 1, '5', 'Nagyon jó óra volt, végre megértettem mindent amiről kérdezni szerettem volna akkoriban.', NULL),
(9, 14, '5', 'Teljes mértékben megértettem a témakört, mivel nehézségeim voltak!', '2026-10-08 10:13:38');

-- --------------------------------------------------------

--
-- Table structure for table `student_subjects`
--

CREATE TABLE `student_subjects` (
  `student_id` int(11) NOT NULL,
  `subject_id` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Dumping data for table `student_subjects`
--

INSERT INTO `student_subjects` (`student_id`, `subject_id`) VALUES
(13, 1);

-- --------------------------------------------------------

--
-- Table structure for table `subjects`
--

CREATE TABLE `subjects` (
  `id` int(60) NOT NULL,
  `name` varchar(30) NOT NULL,
  `category` varchar(30) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- Dumping data for table `subjects`
--

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

--
-- Table structure for table `tutor_subjects`
--

CREATE TABLE `tutor_subjects` (
  `tutor_id` int(11) NOT NULL,
  `subject_id` int(11) NOT NULL,
  `hourly_rate` int(10) UNSIGNED NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- Dumping data for table `tutor_subjects`
--

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
(8, 18, 3200),
(14, 7, 4600),
(14, 8, 1),
(14, 15, 3000);

-- --------------------------------------------------------

--
-- Table structure for table `users`
--

CREATE TABLE `users` (
  `id` int(10) NOT NULL,
  `full_name` varchar(60) NOT NULL,
  `email` varchar(100) NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `role` enum('STUDENT','TUTOR','ADMIN','') NOT NULL,
  `bio` text NOT NULL,
  `hourly_rate` decimal(65,0) NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `school_level` varchar(30) DEFAULT NULL,
  `grade` varchar(30) DEFAULT NULL,
  `deleted_at` timestamp NULL DEFAULT NULL,
  `email_verified_at` timestamp NULL DEFAULT NULL,
  `suspended_at` timestamp NULL DEFAULT NULL,
  `suspended_reason` varchar(200) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_hungarian_ci;

--
-- Dumping data for table `users`
--

INSERT INTO `users` (`id`, `full_name`, `email`, `password_hash`, `role`, `bio`, `hourly_rate`, `created_at`, `school_level`, `grade`, `deleted_at`, `email_verified_at`, `suspended_at`, `suspended_reason`) VALUES
(1, 'Kovács Bence', 'bence.kovacs@example.com', '$argon2id$v=19$m=15,t=4,p=1$eEQ0d09kYURrVGh1MnNWeg$zA6uO5BCB1bE1CPOGyIUHw', 'STUDENT', 'Programozást és adatbázis-kezelést tanulok.', 0, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(2, 'Nagy Anna', 'anna.nagy@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Matematika és informatika iránt érdeklődöm.', 0, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(3, 'Tóth Márk', 'mark.toth@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Tapasztalt programozó vagyok, főleg JavaScript és Python területén.', 3500, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(4, 'Szabó Petra', 'petra.szabo@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Matematika korrepetálást vállalok középiskolásoknak.', 3000, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(5, 'Horváth Dávid', 'david.horvath@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Programozás, algoritmusok és adatstruktúrák oktatása.', 4000, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(6, 'Varga Eszter', 'eszter.varga@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Egyetemi hallgató, jelenleg webfejlesztést tanulok.', 0, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(7, 'Kiss Gergő', 'gergo.kiss@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Angol nyelv és kommunikáció korrepetálást vállalok.', 2800, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(8, 'Farkas Lilla', 'lilla.farkas@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'TUTOR', 'Középiskolai matematika és fizika oktatás.', 3200, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(9, 'Molnár Ádám', 'adam.molnar@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'STUDENT', 'Informatika szakos hallgató vagyok.', 0, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(10, 'Balogh Zoltán', 'zoltan.balogh@example.com', '$2b$10$abcdefghijklmnopqrstuu1234567890abcdefghi', 'ADMIN', 'TutorConnect rendszergazda.', 0, '2026-09-24 07:34:13', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(11, 'Molnár Bence', 'molnar.bence@example.com', '$2b$10$fakehashbence', 'TUTOR', 'Matematika és fizika korrepetálást vállalok középiskolásoknak.', 3500, '2026-09-25 08:15:14', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(12, 'Kovács Réka', 'kovacs.reka@example.com', '$2b$10$fakehashreka', 'TUTOR', 'Angol és német nyelvből vállalok korrepetálást kezdő és haladó szinten.', 3000, '2026-09-25 08:15:14', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(13, 'test', 'test@gmail.com', '$argon2id$v=19$m=32768,p=1,t=4$X401oEo08mY6fHy5P0htpA$gTwWRwTVZnEY9OuY40QR9JlknDDdMIQ5pU643f1gO+E', 'STUDENT', '', 0, '2026-10-08 09:53:51', 'Általános iskola', '8. osztály', NULL, '2026-10-14 08:59:12', NULL, NULL),
(14, 'test2', 'test2@gmail.com', '$argon2id$v=19$m=32768,p=1,t=4$QInUdviilpceEDBys2Xp6A$udWd3gCpgQB/qer9ZAamYAFKEPgAZYEGaDbWxsISv6g', 'TUTOR', 'kuvaszológia professzor', 6700, '2026-09-29 07:17:01', NULL, NULL, NULL, '2026-10-14 08:59:12', NULL, NULL),
(69, 'MM Food', 'mfdoom@gmail.com', '$argon2id$v=19$m=32768,p=1,t=4$SfHmW8jqJj54pY4Kw73q6w$okk8VRBoktPbPquhxBTuJUJsOZDPiTfE0wG6/7JoeMo', 'ADMIN', '', 0, '2026-10-21 08:28:52', NULL, NULL, NULL, '2026-10-21 08:28:58', NULL, NULL);

--
-- Indexes for dumped tables
--

--
-- Indexes for table `admin_log`
--
ALTER TABLE `admin_log`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_admin_log_created` (`created_at`),
  ADD KEY `fk_admin_log_admin` (`admin_id`);

--
-- Indexes for table `auth_tokens`
--
ALTER TABLE `auth_tokens`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uq_token_hash` (`token_hash`),
  ADD KEY `idx_user_type` (`user_id`,`type`);

--
-- Indexes for table `availabilities`
--
ALTER TABLE `availabilities`
  ADD PRIMARY KEY (`id`),
  ADD KEY `tutor_id` (`tutor_id`);

--
-- Indexes for table `bookings`
--
ALTER TABLE `bookings`
  ADD PRIMARY KEY (`id`),
  ADD KEY `student_id` (`student_id`),
  ADD KEY `tutor_id` (`tutor_id`),
  ADD KEY `subject_id` (`subject_id`);

--
-- Indexes for table `conversations`
--
ALTER TABLE `conversations`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `unique_student_tutor` (`student_id`,`tutor_id`),
  ADD KEY `fk_conversation_tutor` (`tutor_id`);

--
-- Indexes for table `messages`
--
ALTER TABLE `messages`
  ADD PRIMARY KEY (`id`),
  ADD KEY `fk_message_conversation` (`conversation_id`),
  ADD KEY `fk_message_sender` (`sender_id`);

--
-- Indexes for table `reports`
--
ALTER TABLE `reports`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_reports_status` (`status`,`created_at`),
  ADD KEY `idx_reports_reported` (`reported_user_id`),
  ADD KEY `fk_reports_reporter` (`reporter_id`);

--
-- Indexes for table `reviews`
--
ALTER TABLE `reviews`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `booking_id` (`booking_id`);

--
-- Indexes for table `student_subjects`
--
ALTER TABLE `student_subjects`
  ADD PRIMARY KEY (`student_id`,`subject_id`);

--
-- Indexes for table `subjects`
--
ALTER TABLE `subjects`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `name` (`name`);

--
-- Indexes for table `tutor_subjects`
--
ALTER TABLE `tutor_subjects`
  ADD PRIMARY KEY (`tutor_id`,`subject_id`),
  ADD KEY `subject_id` (`subject_id`);

--
-- Indexes for table `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `email` (`email`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `admin_log`
--
ALTER TABLE `admin_log`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=19;

--
-- AUTO_INCREMENT for table `auth_tokens`
--
ALTER TABLE `auth_tokens`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=43;

--
-- AUTO_INCREMENT for table `availabilities`
--
ALTER TABLE `availabilities`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=33;

--
-- AUTO_INCREMENT for table `bookings`
--
ALTER TABLE `bookings`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=39;

--
-- AUTO_INCREMENT for table `conversations`
--
ALTER TABLE `conversations`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=12;

--
-- AUTO_INCREMENT for table `messages`
--
ALTER TABLE `messages`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=60;

--
-- AUTO_INCREMENT for table `reports`
--
ALTER TABLE `reports`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=10;

--
-- AUTO_INCREMENT for table `reviews`
--
ALTER TABLE `reviews`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=10;

--
-- AUTO_INCREMENT for table `subjects`
--
ALTER TABLE `subjects`
  MODIFY `id` int(60) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=43;

--
-- AUTO_INCREMENT for table `users`
--
ALTER TABLE `users`
  MODIFY `id` int(10) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=70;

--
-- Constraints for dumped tables
--

--
-- Constraints for table `admin_log`
--
ALTER TABLE `admin_log`
  ADD CONSTRAINT `fk_admin_log_admin` FOREIGN KEY (`admin_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Constraints for table `auth_tokens`
--
ALTER TABLE `auth_tokens`
  ADD CONSTRAINT `fk_auth_tokens_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Constraints for table `availabilities`
--
ALTER TABLE `availabilities`
  ADD CONSTRAINT `availabilities_ibfk_1` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`);

--
-- Constraints for table `bookings`
--
ALTER TABLE `bookings`
  ADD CONSTRAINT `bookings_ibfk_1` FOREIGN KEY (`student_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `bookings_ibfk_2` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `bookings_ibfk_3` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`);

--
-- Constraints for table `conversations`
--
ALTER TABLE `conversations`
  ADD CONSTRAINT `fk_conversation_student` FOREIGN KEY (`student_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_conversation_tutor` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Constraints for table `messages`
--
ALTER TABLE `messages`
  ADD CONSTRAINT `fk_message_conversation` FOREIGN KEY (`conversation_id`) REFERENCES `conversations` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_message_sender` FOREIGN KEY (`sender_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Constraints for table `reports`
--
ALTER TABLE `reports`
  ADD CONSTRAINT `fk_reports_reported` FOREIGN KEY (`reported_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_reports_reporter` FOREIGN KEY (`reporter_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Constraints for table `reviews`
--
ALTER TABLE `reviews`
  ADD CONSTRAINT `reviews_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`);

--
-- Constraints for table `tutor_subjects`
--
ALTER TABLE `tutor_subjects`
  ADD CONSTRAINT `tutor_subjects_ibfk_1` FOREIGN KEY (`tutor_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `tutor_subjects_ibfk_2` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
